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
                new InvoiceLineDto("Annual licence 2026-27", 850, 70, 0),
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
}
