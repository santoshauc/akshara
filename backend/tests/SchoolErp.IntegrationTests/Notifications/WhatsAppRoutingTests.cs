using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Attendance;
using SchoolErp.Domain.Outbox;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Notifications;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.IntegrationTests.Auth;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Notifications;

/// <summary>WhatsApp recorder that can be told to fail, to prove the SMS fallback.</summary>
public sealed class ToggleWhatsAppSender : IWhatsAppSender
{
    private readonly List<(string Phone, string Message)> _sent = [];

    public bool Fail { get; set; }

    public IReadOnlyList<(string Phone, string Message)> Sent => _sent;

    public Task SendAsync(string phone, string message, CancellationToken ct = default)
    {
        if (Fail)
        {
            throw new InvalidOperationException("Simulated WhatsApp outage.");
        }

        _sent.Add((phone, message));
        return Task.CompletedTask;
    }
}

/// <summary>Two schools — one WhatsApp-first, one SMS-only — sharing a dispatcher.</summary>
public sealed class WhatsAppRoutingFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_whatsapp_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid WhatsAppTenantId { get; } = Guid.NewGuid();

    public Guid SmsOnlyTenantId { get; } = Guid.NewGuid();

    public RecordingSmsSender SmsSender { get; } = new();

    public ToggleWhatsAppSender WhatsAppSender { get; } = new();

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
        services.AddScoped<ICurrentUser, StubCurrentUser>();
        services.AddSingleton<ISmsSender>(SmsSender);
        services.AddSingleton<IWhatsAppSender>(WhatsAppSender);
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = WhatsAppTenantId,
                Code = "WA01",
                Name = "WhatsApp School",
                Subdomain = "watest",
                Status = TenantStatus.Active,
                SmsCredits = 10,
                WhatsAppEnabled = true,
            },
            new Tenant
            {
                Id = SmsOnlyTenantId,
                Code = "SMS01",
                Name = "SMS School",
                Subdomain = "smstest",
                Status = TenantStatus.Active,
                SmsCredits = 10,
                WhatsAppEnabled = false,
            });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    public AsyncServiceScope CreateScope() => _provider.CreateAsyncScope();

    public async Task<Guid> QueueSmsAsync(Guid tenantId, string phone, string message)
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = new OutboxMessage
        {
            TenantId = tenantId,
            Type = OutboxMessageTypes.Sms,
            Payload = JsonSerializer.Serialize(new SmsPayload(phone, message)),
        };
        db.OutboxMessages.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    public async Task<int> RunDispatcherAsync()
    {
        await using var scope = CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
        return await processor.ProcessPendingAsync();
    }

    public async Task<int> CreditsAsync(Guid tenantId)
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.SmsCredits)
            .SingleAsync();
    }

    public async Task<OutboxMessage> MessageAsync(Guid messageId)
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == messageId);
    }
}

/// <summary>Channel routing: WhatsApp-first per school, SMS fallback, credits.</summary>
public sealed class WhatsAppRoutingTests : IClassFixture<WhatsAppRoutingFixture>
{
    private readonly WhatsAppRoutingFixture _fixture;

    public WhatsAppRoutingTests(WhatsAppRoutingFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task WhatsApp_school_delivers_on_whatsapp_and_spends_no_sms_credit()
    {
        _fixture.WhatsAppSender.Fail = false;
        // Tests share the fixture — assert credit DELTAS, never absolutes.
        var before = await _fixture.CreditsAsync(_fixture.WhatsAppTenantId);
        var id = await _fixture.QueueSmsAsync(
            _fixture.WhatsAppTenantId, "+919700001111", "Fee reminder: Rs 5,000 due.");

        await _fixture.RunDispatcherAsync();

        _fixture.WhatsAppSender.Sent.Should().Contain(s =>
            s.Phone == "+919700001111" && s.Message.Contains("Fee reminder"));
        _fixture.SmsSender.Sent.Should().NotContain(s => s.Phone == "+919700001111");

        (await _fixture.CreditsAsync(_fixture.WhatsAppTenantId))
            .Should().Be(before, "WhatsApp delivery must not spend SMS credits");
        (await _fixture.MessageAsync(id)).ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task WhatsApp_outage_falls_back_to_sms_and_spends_a_credit()
    {
        _fixture.WhatsAppSender.Fail = true;
        try
        {
            var before = await _fixture.CreditsAsync(_fixture.WhatsAppTenantId);
            var id = await _fixture.QueueSmsAsync(
                _fixture.WhatsAppTenantId, "+919700002222", "Absence noted for Ravi.");

            await _fixture.RunDispatcherAsync();

            _fixture.SmsSender.Sent.Should().Contain(s =>
                s.Phone == "+919700002222" && s.Message.Contains("Absence"));

            (await _fixture.CreditsAsync(_fixture.WhatsAppTenantId))
                .Should().Be(before - 1, "the SMS fallback is metered");
            (await _fixture.MessageAsync(id)).ProcessedAt
                .Should().NotBeNull("the parent was still notified");
        }
        finally
        {
            _fixture.WhatsAppSender.Fail = false;
        }
    }

    [Fact]
    public async Task Sms_only_school_never_touches_whatsapp()
    {
        var before = await _fixture.CreditsAsync(_fixture.SmsOnlyTenantId);
        var id = await _fixture.QueueSmsAsync(
            _fixture.SmsOnlyTenantId, "+919700003333", "Notice: PTM on Friday.");

        await _fixture.RunDispatcherAsync();

        _fixture.WhatsAppSender.Sent.Should().NotContain(s => s.Phone == "+919700003333");
        _fixture.SmsSender.Sent.Should().Contain(s => s.Phone == "+919700003333");

        (await _fixture.CreditsAsync(_fixture.SmsOnlyTenantId))
            .Should().Be(before - 1, "SMS-only schools are metered per message");
        (await _fixture.MessageAsync(id)).ProcessedAt.Should().NotBeNull();
    }
}
