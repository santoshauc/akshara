using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Fees;
using SchoolErp.Application.Fees.Commands;
using SchoolErp.Application.Fees.Queries;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Fees;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Payments;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Auth;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Fees;

/// <summary>One school with a class fee plan and one admitted student.</summary>
public sealed class FeesModuleFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_fees_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid YearId { get; private set; }

    public Guid ClassId { get; private set; }

    public Guid StudentId { get; private set; }

    public Guid TuitionHeadId { get; private set; }

    public Guid TransportHeadId { get; private set; }

    public RecordingSmsSender SmsSender { get; } = new();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _container.GetConnectionString(),
                ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789abcdef",
                ["Payments:WebhookSecret"] = "test-webhook-secret",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser, StubCurrentUser>();
        services.AddSingleton<ISmsSender>(SmsSender);
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "FEES01",
                Name = "Fees Test School",
                Subdomain = "feestest",
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
            YearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;

            var grade3 = await sender.Send(new CreateClassCommand("Grade 3", 3, ["A"]));
            ClassId = grade3.Id;
            var sectionId = grade3.Sections.Single().Id;

            TuitionHeadId = (await sender.Send(new CreateFeeHeadCommand("Tuition"))).Id;
            TransportHeadId = (await sender.Send(new CreateFeeHeadCommand("Transport"))).Id;

            // Tuition in two installments of 15000, transport one 6000.
            await sender.Send(new DefineFeeStructureCommand(YearId, ClassId,
            [
                new FeeStructureItemInput(TuitionHeadId, 15000, new DateOnly(2026, 7, 10)),
                new FeeStructureItemInput(TuitionHeadId, 15000, new DateOnly(2026, 12, 10)),
                new FeeStructureItemInput(TransportHeadId, 6000, new DateOnly(2026, 7, 10)),
            ]));

            StudentId = await sender.Send(new AdmitStudentCommand(
                null, "Dhruv", "Patel", new DateOnly(2018, 2, 20), Gender.Male,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), YearId, ClassId, sectionId, 1,
                [new GuardianInput("Nita", "Patel", GuardianRelation.Mother, "+919500000001", null, null, true)]));
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

    /// <summary>Scope with NO tenant — how the gateway webhook arrives.</summary>
    public AsyncServiceScope CreateWebhookScope() => _provider.CreateAsyncScope();
}

/// <summary>Fees behavior through the full pipeline, gateway webhook included.</summary>
public sealed class FeesModuleTests : IClassFixture<FeesModuleFixture>
{
    private readonly FeesModuleFixture _fixture;

    public FeesModuleTests(FeesModuleFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Summary_shows_dues_payments_and_balance()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Fixture state is shared across tests — assert deltas, not absolutes.
        var before = await sender.Send(new GetStudentFeeSummaryQuery(_fixture.StudentId, _fixture.YearId));

        var receipt = await sender.Send(new RecordPaymentCommand(
            _fixture.StudentId, _fixture.YearId, 10000,
            new DateOnly(2026, 7, 5), PaymentMode.Cash, null, null));
        receipt.ReceiptNumber.Should().MatchRegex(@"^RCP-2026-\d{4}$");

        var summary = await sender.Send(new GetStudentFeeSummaryQuery(_fixture.StudentId, _fixture.YearId));
        summary.TotalDue.Should().Be(36000);
        summary.TotalPaid.Should().Be(before.TotalPaid + 10000);
        summary.Balance.Should().Be(36000 - summary.TotalPaid);
        summary.DueLines.Should().HaveCount(3);
        summary.Payments.Should().Contain(p => p.Mode == PaymentMode.Cash && p.Amount == 10000);
    }

    [Fact]
    public async Task Payment_queues_a_receipt_sms_for_the_primary_guardian()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var receipt = await sender.Send(new RecordPaymentCommand(
            _fixture.StudentId, _fixture.YearId, 5000,
            new DateOnly(2026, 7, 6), PaymentMode.Upi, "upi-ref-1", null));

        var outbox = await db.OutboxMessages
            .Where(m => m.TenantId == _fixture.TenantId && m.ProcessedAt == null)
            .ToListAsync();
        outbox.Should().Contain(m =>
            m.Payload.Contains(receipt.ReceiptNumber) && m.Payload.Contains("Dhruv"));
    }

    [Fact]
    public async Task Online_flow_order_then_signed_webhook_records_the_payment()
    {
        Guid orderId;
        string gatewayOrderId;

        await using (var scope = _fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var order = await sender.Send(new CreatePaymentOrderCommand(
                _fixture.StudentId, _fixture.YearId, 7500));
            orderId = order.OrderId;
            gatewayOrderId = order.GatewayOrderId;
        }

        // Webhook arrives with no tenant scope, signed with the shared secret.
        var body = JsonSerializer.Serialize(new DevPaymentGateway.DevWebhookBody(
            gatewayOrderId, "dev_pay_001", "paid"));
        var signature = DevPaymentGateway.Sign(body, "test-webhook-secret");

        await using (var webhookScope = _fixture.CreateWebhookScope())
        {
            var processor = webhookScope.ServiceProvider.GetRequiredService<GatewayWebhookProcessor>();
            (await processor.ProcessAsync(body, signature)).Should().BeTrue();
            // Duplicate delivery must be idempotent.
            (await processor.ProcessAsync(body, signature)).Should().BeTrue();
        }

        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.PaymentOrders.SingleAsync(o => o.Id == orderId);
            order.Status.Should().Be(PaymentOrderStatus.Paid);

            var payments = await db.FeePayments
                .Where(p => p.StudentId == _fixture.StudentId && p.Mode == PaymentMode.Online)
                .ToListAsync();
            payments.Should().ContainSingle("duplicate webhooks must not double-record")
                .Which.Reference.Should().Be("dev_pay_001");
        }
    }

    [Fact]
    public async Task Webhook_with_a_bad_signature_is_rejected()
    {
        var body = JsonSerializer.Serialize(new DevPaymentGateway.DevWebhookBody(
            "dev_order_whatever", "dev_pay_x", "paid"));

        await using var webhookScope = _fixture.CreateWebhookScope();
        var processor = webhookScope.ServiceProvider.GetRequiredService<GatewayWebhookProcessor>();
        (await processor.ProcessAsync(body, "forged-signature")).Should().BeFalse();
    }

    [Fact]
    public async Task Fines_concessions_receipt_and_reminders_work_end_to_end()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Own class + heads so the shared plan's totals stay untouched.
        var polishClass = await sender.Send(new CreateClassCommand("Polish 9", 9, ["A"]));
        var flatHead = await sender.Send(new CreateFeeHeadCommand(
            "PolishTuition", LateFineType.Flat, 200));
        var percentHead = await sender.Send(new CreateFeeHeadCommand(
            "PolishLab", LateFineType.Percent, 10));
        await sender.Send(new DefineFeeStructureCommand(_fixture.YearId, polishClass.Id,
        [
            new FeeStructureItemInput(flatHead.Id, 10_000, new DateOnly(2026, 7, 1)),
            new FeeStructureItemInput(percentHead.Id, 2_000, new DateOnly(2026, 7, 1)),
            new FeeStructureItemInput(flatHead.Id, 5_000, new DateOnly(2027, 1, 10)),
        ]));

        var studentId = await sender.Send(new AdmitStudentCommand(
            null, "Pia", "Polish", new DateOnly(2017, 5, 5), Gender.Female,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 6, 5), _fixture.YearId, polishClass.Id,
            polishClass.Sections.Single().Id, 1,
            [new GuardianInput("Rima", "Polish", GuardianRelation.Mother, "+919500000777", null, null, true)]));

        // Overdue lines accrue their fines: flat 200 + 10% of 2000 = 400 total.
        var summary = await sender.Send(new GetStudentFeeSummaryQuery(studentId, _fixture.YearId));
        summary.TotalLateFine.Should().Be(400);
        summary.TotalDue.Should().Be(17_000 + 400);

        // A concession and a payment both reduce the balance.
        var concessionId = await sender.Send(new GrantConcessionCommand(
            studentId, _fixture.YearId, null, 1_000, "Sibling discount"));
        var receipt = await sender.Send(new RecordPaymentCommand(
            studentId, _fixture.YearId, 5_000,
            new DateOnly(2026, 8, 8), PaymentMode.Cash, null, null));

        summary = await sender.Send(new GetStudentFeeSummaryQuery(studentId, _fixture.YearId));
        summary.TotalConcession.Should().Be(1_000);
        summary.Balance.Should().Be(17_400 - 1_000 - 5_000);

        // The payment's receipt renders as a real PDF.
        var pdf = await sender.Send(new GetReceiptPdfQuery(receipt.PaymentId));
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");

        // Revoking the concession restores the balance.
        await sender.Send(new RevokeConcessionCommand(concessionId));
        (await sender.Send(new GetStudentFeeSummaryQuery(studentId, _fixture.YearId)))
            .Balance.Should().Be(17_400 - 5_000);

        // The nightly job queues one reminder SMS for the overdue balance —
        // and re-running within 6 days does not queue a duplicate.
        var job = scope.ServiceProvider
            .GetRequiredService<SchoolErp.Infrastructure.Notifications.FeeReminderJob>();
        await job.RunAsync(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        async Task<List<string>> PiaRemindersAsync() =>
            (await db.OutboxMessages.AsNoTracking()
                .Where(m => m.TenantId == _fixture.TenantId)
                .Select(m => m.Payload)
                .ToListAsync())
            .Where(p => p.Contains("Fee reminder") && p.Contains("+919500000777"))
            .ToList();

        (await PiaRemindersAsync()).Should().ContainSingle()
            .Which.Should().Contain("Pia Polish");

        await job.RunAsync(CancellationToken.None);
        (await PiaRemindersAsync())
            .Should().HaveCount(1, "a guardian is reminded at most once per 6 days");
    }

    [Fact]
    public async Task Family_fee_view_lists_all_siblings_once_with_combined_total()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // A sibling admitted with the SAME guardian phone links to Nita Patel.
        var sibling = await sender.Send(new AdmitStudentCommand(
            null, "Divya", "Patel", new DateOnly(2016, 6, 15), Gender.Female,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 6, 5), _fixture.YearId, _fixture.ClassId,
            (await sender.Send(new GetClassesQuery()))
                .Single(c => c.Id == _fixture.ClassId).Sections.Single().Id,
            5,
            [new GuardianInput("Nita", "Patel", GuardianRelation.Mother, "+919500000001", null, null, true)]));

        // Staff view from EITHER sibling shows the whole family exactly once.
        var fromDhruv = await sender.Send(new GetStudentFamilyFeesQuery(_fixture.StudentId));
        fromDhruv.Children.Should().HaveCount(2)
            .And.OnlyHaveUniqueItems(c => c.StudentId);
        fromDhruv.Children.Select(c => c.StudentName)
            .Should().Contain(["Dhruv Patel", "Divya Patel"]);
        fromDhruv.FamilyBalance.Should().Be(fromDhruv.Children.Sum(c => c.Balance));

        var fromSibling = await sender.Send(new GetStudentFamilyFeesQuery(sibling));
        fromSibling.Children.Should().HaveCount(2);

        // The parent-side view (resolved by guardian phone) matches.
        var parentView = await sender.Send(
            new GetParentFamilyFeesQuery(null, "+919500000001"));
        parentView.Children.Should().HaveCount(2);
        parentView.FamilyBalance.Should().Be(fromDhruv.FamilyBalance);
    }

    [Fact]
    public async Task Manual_online_mode_is_rejected_and_unknown_student_404s()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var onlineManual = () => sender.Send(new RecordPaymentCommand(
            _fixture.StudentId, _fixture.YearId, 100,
            new DateOnly(2026, 7, 7), PaymentMode.Online, null, null));
        await onlineManual.Should().ThrowAsync<FluentValidation.ValidationException>();

        var unknownStudent = () => sender.Send(new RecordPaymentCommand(
            Guid.NewGuid(), _fixture.YearId, 100,
            new DateOnly(2026, 7, 7), PaymentMode.Cash, null, null));
        await unknownStudent.Should().ThrowAsync<NotFoundException>();
    }
}
