using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Platform;
using SchoolErp.Domain.Billing;
using SchoolErp.Domain.Campuses;
using SchoolErp.Domain.Staff;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Platform;

/// <summary>
/// Two schools with different contents, so an aggregate that quietly returned
/// one school's figures — or zero, which is what RLS does to a cross-tenant
/// query — cannot pass.
/// </summary>
public sealed class PlatformDashboardFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_platform_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    /// <summary>Active, Standard plan, 3 students / 2 teachers / 2 guardians / 2 campuses.</summary>
    public Guid BusyTenantId { get; } = Guid.NewGuid();

    /// <summary>Active college, no students at all, and an overdue invoice.</summary>
    public Guid EmptyTenantId { get; } = Guid.NewGuid();

    /// <summary>Suspended, so it must show up in the attention centre.</summary>
    public Guid SuspendedTenantId { get; } = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _container.GetConnectionString(),
                ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789abcdef",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<GuidCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<GuidCurrentUser>());
        _provider = services.BuildServiceProvider(validateScopes: true);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();

            db.Tenants.Add(new Tenant
            {
                Id = BusyTenantId,
                Code = "PDBUSY",
                Name = "Busy Public School",
                Subdomain = "pdbusy",
                Plan = SubscriptionPlan.Standard,
                Status = TenantStatus.Active,
                SmsCredits = 10_000,
                SubscriptionExpiresOn = today.AddDays(200),
            });
            db.Tenants.Add(new Tenant
            {
                Id = EmptyTenantId,
                Code = "PDEMPT",
                Name = "Empty Degree College",
                Subdomain = "pdempt",
                InstitutionType = InstitutionType.College,
                Plan = SubscriptionPlan.Basic,
                Status = TenantStatus.Active,
                SmsCredits = 100,
                SubscriptionExpiresOn = today.AddDays(10),
            });
            db.Tenants.Add(new Tenant
            {
                Id = SuspendedTenantId,
                Code = "PDSUSP",
                Name = "Suspended School",
                Subdomain = "pdsusp",
                Plan = SubscriptionPlan.Basic,
                Status = TenantStatus.Suspended,
                SmsCredits = 5_000,
            });

            // ₹40,000 issued and long past due against the college.
            db.Invoices.Add(new Invoice
            {
                TenantId = EmptyTenantId,
                InvoiceNumber = "INV-TEST-0001",
                Status = InvoiceStatus.Issued,
                IssuedOn = today.AddDays(-60),
                DueOn = today.AddDays(-30),
                TotalAmount = 40_000m,
            });
            // ₹25,000 already settled, inside the window.
            db.Invoices.Add(new Invoice
            {
                TenantId = BusyTenantId,
                InvoiceNumber = "INV-TEST-0002",
                Status = InvoiceStatus.Paid,
                IssuedOn = today.AddDays(-10),
                DueOn = today.AddDays(20),
                PaidOn = today.AddDays(-5),
                TotalAmount = 25_000m,
            });

            await db.SaveChangesAsync();
        }

        await SeedBusySchoolAsync();
    }

    /// <summary>
    /// Written through a tenant-bound scope so the interceptor stamps
    /// tenant_id exactly the way the application would.
    /// </summary>
    private async Task SeedBusySchoolAsync()
    {
        await using var scope = CreateScope(BusyTenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var i = 1; i <= 3; i++)
        {
            db.Students.Add(new Student
            {
                AdmissionNumber = $"PD-{i:D3}",
                FirstName = "Student",
                LastName = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                DateOfBirth = new DateOnly(2015, 1, i),
                Gender = Gender.Female,
                AdmissionDate = new DateOnly(2026, 6, 1),
                Status = StudentStatus.Active,
            });
        }

        // Withdrawn students are still rows; the dashboard must not count them.
        db.Students.Add(new Student
        {
            AdmissionNumber = "PD-GONE",
            FirstName = "Left",
            LastName = "Already",
            DateOfBirth = new DateOnly(2014, 5, 5),
            Gender = Gender.Male,
            AdmissionDate = new DateOnly(2025, 6, 1),
            Status = StudentStatus.Withdrawn,
        });

        db.Teachers.Add(new Teacher { EmployeeCode = "PD-T1", FullName = "Teacher One", Phone = "+919000000001" });
        db.Teachers.Add(new Teacher { EmployeeCode = "PD-T2", FullName = "Teacher Two", Phone = "+919000000002" });
        db.Teachers.Add(new Teacher
        {
            EmployeeCode = "PD-T3",
            FullName = "Retired Teacher",
            Phone = "+919000000003",
            IsActive = false,
        });

        db.Guardians.Add(new Guardian
        {
            FirstName = "Guardian", LastName = "One",
            Relation = GuardianRelation.Mother, Phone = "+919100000001",
        });
        db.Guardians.Add(new Guardian
        {
            FirstName = "Guardian", LastName = "Two",
            Relation = GuardianRelation.Father, Phone = "+919100000002",
        });

        db.Campuses.Add(new Campus { Name = "Main", Code = "MAIN", IsPrimary = true, IsActive = true });
        db.Campuses.Add(new Campus { Name = "Annexe", Code = "ANX", IsPrimary = false, IsActive = true });
        db.Campuses.Add(new Campus { Name = "Old Block", Code = "OLD", IsPrimary = false, IsActive = false });

        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>A platform scope: no tenant bound, exactly like a Super Admin request.</summary>
    public AsyncServiceScope CreatePlatformScope() => _provider.CreateAsyncScope();

    public AsyncServiceScope CreateScope(Guid tenantId)
    {
        var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(tenantId);
        return scope;
    }
}

/// <summary>The figures a Super Admin sees have to be the real ones.</summary>
public sealed class PlatformDashboardTests : IClassFixture<PlatformDashboardFixture>
{
    private readonly PlatformDashboardFixture _fixture;

    public PlatformDashboardTests(PlatformDashboardFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task People_are_counted_across_tenants_despite_row_level_security()
    {
        await using var scope = _fixture.CreatePlatformScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var dashboard = await sender.Send(new GetPlatformDashboardQuery());

        // The whole point: a caller with NO tenant bound still sees the totals.
        dashboard.People.Students.Should().Be(3, "withdrawn students are not enrolled");
        dashboard.People.Teachers.Should().Be(2, "the retired teacher is inactive");
        dashboard.People.Guardians.Should().Be(2);
        dashboard.People.Campuses.Should().Be(2, "the closed block does not count");
    }

    [Fact]
    public async Task Institutions_are_summarised_by_state_and_kind()
    {
        await using var scope = _fixture.CreatePlatformScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var dashboard = await sender.Send(new GetPlatformDashboardQuery());

        dashboard.Overview.Total.Should().Be(3);
        dashboard.Overview.Active.Should().Be(2);
        dashboard.Overview.Suspended.Should().Be(1);
        dashboard.Overview.Schools.Should().Be(2);
        dashboard.Overview.Colleges.Should().Be(1);
    }

    [Fact]
    public async Task Each_institution_row_carries_its_own_figures()
    {
        await using var scope = _fixture.CreatePlatformScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var dashboard = await sender.Send(new GetPlatformDashboardQuery());

        var busy = dashboard.Institutions.Single(r => r.Code == "PDBUSY");
        busy.Students.Should().Be(3);
        busy.Teachers.Should().Be(2);
        busy.Campuses.Should().Be(2);
        busy.Outstanding.Should().Be(0m, "its only invoice is paid");

        var college = dashboard.Institutions.Single(r => r.Code == "PDEMPT");
        college.Students.Should().Be(0);
        college.InstitutionType.Should().Be(InstitutionType.College);
        college.Outstanding.Should().Be(40_000m);
    }

    [Fact]
    public async Task Money_comes_from_invoices_and_the_estimate_is_derived_not_invented()
    {
        await using var scope = _fixture.CreatePlatformScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var dashboard = await sender.Send(new GetPlatformDashboardQuery(WindowDays: 30));

        dashboard.Revenue.Outstanding.Should().Be(40_000m);
        dashboard.Revenue.Overdue.Should().Be(40_000m);
        dashboard.Revenue.OverdueInvoices.Should().Be(1);
        dashboard.Revenue.CollectedInWindow.Should().Be(25_000m);
        dashboard.Revenue.BilledInWindow.Should().Be(
            25_000m, "the overdue invoice was issued 60 days ago, outside the window");

        // Standard list rate (₹70) × 3 enrolled students; the college has none
        // and the suspended school is not live.
        dashboard.Revenue.AnnualisedLicenceValue.Should().Be(210m);
    }

    [Fact]
    public async Task The_attention_centre_names_the_real_problems()
    {
        await using var scope = _fixture.CreatePlatformScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var dashboard = await sender.Send(new GetPlatformDashboardQuery());

        dashboard.Attention.Should().Contain(a =>
            a.Title == "School suspended" && a.TenantName == "Suspended School");
        dashboard.Attention.Should().Contain(a =>
            a.Title == "Unpaid invoices" && a.TenantName == "Empty Degree College");
        dashboard.Attention.Should().Contain(a =>
            a.Title == "SMS credits low" && a.TenantName == "Empty Degree College");
        dashboard.Attention.Should().Contain(a =>
            a.Title == "No students yet" && a.TenantName == "Empty Degree College");
        dashboard.Attention.Should().Contain(a =>
            a.Title == "Renewal due" && a.TenantName == "Empty Degree College");

        // The healthy school has nothing wrong with it.
        dashboard.Attention.Should().NotContain(a => a.TenantName == "Busy Public School");

        // Most severe first, so the top of the list is the thing to do next.
        dashboard.Attention[0].Severity.Should().Be(AttentionSeverity.Critical);
    }

    [Fact]
    public async Task Metrics_the_platform_cannot_measure_are_declared_rather_than_guessed()
    {
        await using var scope = _fixture.CreatePlatformScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var dashboard = await sender.Send(new GetPlatformDashboardQuery());

        dashboard.UnavailableMetrics.Should().NotBeEmpty();
        dashboard.UnavailableMetrics.Should().Contain(m => m.Contains("MRR/ARR", StringComparison.Ordinal));
        dashboard.Growth.Should().HaveCount(12, "a year of months, gaps included as zero");
    }
}
