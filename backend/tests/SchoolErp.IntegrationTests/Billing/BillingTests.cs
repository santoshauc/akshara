using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Billing;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Billing;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Billing;

/// <summary>One school to bill.</summary>
public sealed class BillingFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_billing_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    /// <summary>
    /// The live configuration root. The GST test writes the operator's
    /// registration here and restores it in a finally — the tax profile reads
    /// configuration per access precisely so registration can arrive without a
    /// restart, and this is that same mechanism.
    /// </summary>
    public IConfigurationRoot Configuration { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _container.GetConnectionString(),
                ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789abcdef",
                // Make the billing cycle exercisable today, whatever today is.
                ["Billing:RenewalMonth"] = DateTime.UtcNow.Month.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                ["Billing:AutoSuspend"] = "true",
                ["Billing:SuspendGraceDays"] = "0",
            })
            .Build();
        Configuration = configuration;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<GuidCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<GuidCurrentUser>());
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "BILL01",
                Name = "Billing Test School",
                Subdomain = "billingtest",
                Status = TenantStatus.Active,
                SmsCredits = 1_000,
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateAcademicYearCommand(
                "2026-27", new DateOnly(2026, 6, 1), new DateOnly(2027, 4, 30), MakeCurrent: true));
            var schoolClass = await sender.Send(new CreateClassCommand("Grade 1", 1, ["A"]));
            var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;
            await sender.Send(new AdmitStudentCommand(
                null, "Billa", "Rao", new DateOnly(2020, 1, 1), Gender.Male,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), yearId, schoolClass.Id,
                schoolClass.Sections.Single().Id, 1,
                [new GuardianInput("Guardian", "Rao", GuardianRelation.Father, "+919700000900", null, null, true)]));
        }
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    public AsyncServiceScope CreateScope()
    {
        var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(TenantId);
        return scope;
    }
}

/// <summary>Invoices, SMS top-ups and the usage view.</summary>
public sealed class BillingTests : IClassFixture<BillingFixture>
{
    private readonly BillingFixture _fixture;

    public BillingTests(BillingFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Invoice_lifecycle_issue_pay_and_refuse_double_settlement()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var invoice = await sender.Send(new CreateInvoiceCommand(
            _fixture.TenantId,
            new DateOnly(2026, 9, 30),
            [
                new InvoiceLineDto("Manual licence 2026-27", 850, 70, 0),
                new InvoiceLineDto("Onboarding & data import", 1, 15_000, 0),
            ],
            "Season discount applied on setup."));

        invoice.InvoiceNumber.Should().MatchRegex(@"^INV-\d{4}-\d{4}$");
        invoice.TotalAmount.Should().Be(850 * 70 + 15_000, "line amounts come from qty × unit");
        invoice.Status.Should().Be(InvoiceStatus.Issued);
        invoice.SchoolName.Should().Be("Billing Test School");

        await sender.Send(new MarkInvoicePaidCommand(invoice.Id, new DateOnly(2026, 8, 20)));
        var paidAgain = () => sender.Send(new MarkInvoicePaidCommand(invoice.Id, new DateOnly(2026, 8, 21)));
        await paidAgain.Should().ThrowAsync<ConflictException>();
        var voidPaid = () => sender.Send(new VoidInvoiceCommand(invoice.Id));
        await voidPaid.Should().ThrowAsync<ConflictException>("paid invoices stay paid");

        var pdf = await sender.Send(new GetInvoicePdfQuery(invoice.Id));
        pdf.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Sms_topup_credits_and_invoice_move_together_and_usage_reflects_it()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var creditsBefore = (await db.Tenants.AsNoTracking()
            .SingleAsync(t => t.Id == _fixture.TenantId)).SmsCredits;

        var invoice = await sender.Send(new RecordSmsTopUpCommand(
            _fixture.TenantId, Credits: 5_000, UnitPrice: 0.35m, new DateOnly(2026, 9, 15)));

        invoice.TotalAmount.Should().Be(1_750, "5,000 × ₹0.35");
        invoice.Lines.Should().ContainSingle().Which.Description.Should().Contain("5,000 credits");

        var usage = await sender.Send(new GetTenantUsageQuery(_fixture.TenantId));
        usage.SchoolName.Should().Be("Billing Test School");
        usage.SmsCreditsRemaining.Should().Be(creditsBefore + 5_000);
        usage.ActiveStudents.Should().Be(1, "one admitted student");
        usage.OutstandingInvoiceTotal.Should().BeGreaterThanOrEqualTo(1_750,
            "the top-up invoice is still unpaid");

        // Both-school listing filter.
        (await sender.Send(new GetInvoicesQuery(_fixture.TenantId)))
            .Should().Contain(i => i.Id == invoice.Id);
        (await sender.Send(new GetInvoicesQuery(Guid.NewGuid())))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Billing_cycle_renews_licences_once_and_suspends_the_long_overdue()
    {
        await using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = scope.ServiceProvider
            .GetRequiredService<SchoolErp.Infrastructure.Billing.BillingCycleJob>();

        // The fixture school becomes a paid plan so renewal applies to it.
        var school = await db.Tenants.SingleAsync(t => t.Id == _fixture.TenantId);
        school.Plan = SubscriptionPlan.Standard;

        // A second school with a long-overdue invoice awaits suspension.
        var overdue = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = "OVRDU1",
            Name = "Overdue School",
            Subdomain = "overduetest",
            Status = TenantStatus.Active,
        };
        db.Tenants.Add(overdue);
        db.Invoices.Add(new SchoolErp.Domain.Billing.Invoice
        {
            TenantId = overdue.Id,
            InvoiceNumber = "INV-TEST-OVERDUE",
            IssuedOn = new DateOnly(2026, 1, 1),
            DueOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            TotalAmount = 999,
        });
        await db.SaveChangesAsync();

        await job.RunAsync(CancellationToken.None);
        await job.RunAsync(CancellationToken.None); // idempotency

        var licences = (await db.Invoices.AsNoTracking()
                .Include(i => i.Lines)
                .Where(i => i.TenantId == _fixture.TenantId)
                .ToListAsync())
            .Where(i => i.Lines.Any(l =>
                l.Description.StartsWith("Annual licence", StringComparison.Ordinal)))
            .ToList();
        licences.Should().HaveCount(1, "the second run must not double-invoice");
        licences[0].Lines.Single().Quantity.Should().Be(1, "one active student");
        licences[0].Lines.Single().UnitAmount.Should().Be(
            PlanPresets.AnnualRatePerStudent(SubscriptionPlan.Standard));

        (await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == overdue.Id))
            .Status.Should().Be(TenantStatus.Suspended, "its invoice is past the grace period");
        (await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == _fixture.TenantId))
            .Status.Should().Be(TenantStatus.Active, "its invoices are not overdue");
    }

    [Fact]
    public async Task A_registered_operator_issues_tax_invoices_and_the_tax_is_frozen_on_the_row()
    {
        // Registration arrives as configuration, exactly as it does in
        // production. Restored in the finally so the other tests in this class
        // keep issuing the plain invoices their totals assert.
        _fixture.Configuration["Billing:Gstin"] = "36AAAAA0000A1Z5";
        _fixture.Configuration["Billing:GstState"] = "Telangana";
        try
        {
            await using var scope = _fixture.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            // The seeded school has no GSTIN and no state on file, which is the
            // common case - an unidentifiable recipient defaults to intra-state.
            var intra = await sender.Send(new CreateInvoiceCommand(
                _fixture.TenantId, new DateOnly(2026, 9, 30),
                [new InvoiceLineDto("Software subscription (GST check)", 100, 100m, 0)], null));

            intra.Tax.Should().NotBeNull();
            intra.Tax!.TaxableAmount.Should().Be(10_000m);
            intra.Tax.Cgst.Should().Be(900m);
            intra.Tax.Sgst.Should().Be(900m);
            intra.Tax.Igst.Should().Be(0m);
            intra.TotalAmount.Should().Be(11_800m, "total = taxable + CGST + SGST");
            intra.Tax.SacCode.Should().Be("997331");

            // A Karnataka-registered school (GSTIN prefix 29) makes the same
            // supply inter-state: one IGST levy instead of the split.
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var school = await db.Tenants.SingleAsync(t => t.Id == _fixture.TenantId);
            school.Gstin = "29CCCCC2222C1Z7";
            await db.SaveChangesAsync();

            var inter = await sender.Send(new CreateInvoiceCommand(
                _fixture.TenantId, new DateOnly(2026, 9, 30),
                [new InvoiceLineDto("Software subscription (GST check)", 100, 100m, 0)], null));

            inter.Tax!.Igst.Should().Be(1_800m);
            inter.Tax.Cgst.Should().Be(0m);
            inter.Tax.BuyerGstin.Should().Be("29CCCCC2222C1Z7");
            inter.TotalAmount.Should().Be(11_800m);

            // The PDF must still render as a valid document with the breakup on it.
            var pdf = await sender.Send(new GetInvoicePdfQuery(inter.Id));
            System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");

            // FROZEN means frozen: deregistering must not rewrite what an
            // already-issued invoice says. This is the property that makes the
            // row a legal record rather than a view over live configuration.
            _fixture.Configuration["Billing:Gstin"] = "";
            var afterDeregistration = await sender.Send(new GetInvoicesQuery(_fixture.TenantId));
            afterDeregistration.Single(i => i.Id == inter.Id)
                .Tax!.Igst.Should().Be(1_800m);

            // And a NEW invoice issued while unregistered is plain again.
            var plain = await sender.Send(new CreateInvoiceCommand(
                _fixture.TenantId, new DateOnly(2026, 9, 30),
                [new InvoiceLineDto("Onboarding", 1, 5_000m, 0)], null));
            plain.Tax.Should().BeNull();
            plain.TotalAmount.Should().Be(5_000m);
        }
        finally
        {
            _fixture.Configuration["Billing:Gstin"] = "";
            _fixture.Configuration["Billing:GstState"] = "";
            // The seeded school's GSTIN must not leak into other tests' invoices.
            await using var cleanup = _fixture.CreateScope();
            var db = cleanup.ServiceProvider.GetRequiredService<AppDbContext>();
            var school = await db.Tenants.SingleAsync(t => t.Id == _fixture.TenantId);
            school.Gstin = null;
            await db.SaveChangesAsync();
        }
    }
}
