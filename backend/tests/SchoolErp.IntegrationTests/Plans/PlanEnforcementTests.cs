using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Attendance;
using SchoolErp.Application.Auth;
using SchoolErp.Domain.Outbox;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Identity;
using SchoolErp.Infrastructure.Notifications;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.IntegrationTests.Auth;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Plans;

/// <summary>
/// One active school (2 SMS credits, Core-only modules) and one school whose
/// subscription lapsed yesterday — enough to exercise all three A5 gates.
/// </summary>
public sealed class PlanEnforcementFixture : IAsyncLifetime
{
    public const string MeteredCode = "METER1";
    public const string ExpiredCode = "EXPIR1";
    public const string Password = "Admin@12345";
    public const string ExpiredAdminEmail = "admin@expired.school";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_plan_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid MeteredTenantId { get; } = Guid.NewGuid();

    public Guid ExpiredTenantId { get; } = Guid.NewGuid();

    public RecordingSmsSender SmsSender { get; } = new();

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
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser, StubCurrentUser>();
        services.AddSingleton<ISmsSender>(SmsSender);
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        db.Tenants.Add(new Tenant
        {
            Id = MeteredTenantId,
            Code = MeteredCode,
            Name = "Metered School",
            Subdomain = "metered",
            Status = TenantStatus.Active,
            EnabledModules = TenantModules.Core,
            SmsCredits = 2,
        });
        db.Tenants.Add(new Tenant
        {
            Id = ExpiredTenantId,
            Code = ExpiredCode,
            Name = "Expired School",
            Subdomain = "expired",
            Status = TenantStatus.Active,
            SubscriptionExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            SmsCredits = 1_000,
        });
        await db.SaveChangesAsync();

        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = Guid.NewGuid().ToString("N"),
            Email = ExpiredAdminEmail,
            PhoneNumber = "+919812340001",
            FullName = "Expired Admin",
            TenantId = ExpiredTenantId,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
        };
        (await userManager.CreateAsync(admin, Password)).Succeeded
            .Should().BeTrue("seeding the expired-school admin must succeed");
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    public AsyncServiceScope CreateScope() => _provider.CreateAsyncScope();
}

/// <summary>A5 plan enforcement: SMS metering, expiry lockout, module gate.</summary>
public sealed class PlanEnforcementTests : IClassFixture<PlanEnforcementFixture>
{
    private readonly PlanEnforcementFixture _fixture;

    public PlanEnforcementTests(PlanEnforcementFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Outbox_spends_credits_and_dead_letters_when_exhausted()
    {
        await using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var i = 0; i < 3; i++)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                TenantId = _fixture.MeteredTenantId,
                Type = OutboxMessageTypes.Sms,
                Payload = JsonSerializer.Serialize(
                    new SmsPayload($"+91990000000{i}", $"metered message {i}")),
            });
        }
        await db.SaveChangesAsync();

        var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.ProcessPendingAsync();

        // 2 credits → exactly 2 delivered; the third is dead-lettered with a
        // clear reason and no further retries.
        _fixture.SmsSender.Sent.Count(s => s.Message.StartsWith("metered", StringComparison.Ordinal))
            .Should().Be(2);

        var messages = await db.OutboxMessages
            .Where(m => m.TenantId == _fixture.MeteredTenantId)
            .ToListAsync();
        messages.Where(m => m.ProcessedAt != null).Should().HaveCount(2);
        var blocked = messages.Single(m => m.ProcessedAt == null);
        blocked.LastError.Should().Contain("SMS credits");
        blocked.Attempts.Should().Be(5, "blocked sends must not be retried");

        (await db.Tenants.Where(t => t.Id == _fixture.MeteredTenantId)
            .Select(t => t.SmsCredits).SingleAsync())
            .Should().Be(0);
    }

    [Fact]
    public async Task Expired_subscription_blocks_password_login()
    {
        await using var scope = _fixture.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var result = await auth.LoginWithPasswordAsync(
            PlanEnforcementFixture.ExpiredCode,
            PlanEnforcementFixture.ExpiredAdminEmail,
            PlanEnforcementFixture.Password,
            ipAddress: null);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(AuthError.SubscriptionExpired);
    }

    [Fact]
    public async Task Expired_subscription_swallows_otp_requests()
    {
        await using var scope = _fixture.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var before = _fixture.SmsSender.Sent.Count;
        await auth.RequestOtpAsync(PlanEnforcementFixture.ExpiredCode, "+919812340001");

        _fixture.SmsSender.Sent.Count.Should().Be(before, "no OTP SMS for an expired school");
    }

    [Fact]
    public async Task Module_gate_blocks_disabled_module_and_allows_enabled()
    {
        await using var scope = _fixture.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>()
            .SetTenant(_fixture.MeteredTenantId);

        // Core-only school: Library must 403, Core-tagged endpoints pass.
        (await RunGateAsync(scope, TenantModules.Library)).Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await RunGateAsync(scope, TenantModules.Core)).Should().BeNull();
    }

    [Fact]
    public async Task Module_gate_skips_platform_requests()
    {
        // No tenant in scope (Super Admin) — the gate must not interfere.
        await using var scope = _fixture.CreateScope();
        (await RunGateAsync(scope, TenantModules.Library)).Should().BeNull();
    }

    /// <summary>Runs ModuleGateFilter against a fake action; null = passed through.</summary>
    private static async Task<IActionResult?> RunGateAsync(
        AsyncServiceScope scope, TenantModules module)
    {
        var filter = new ModuleGateFilter(
            scope.ServiceProvider.GetRequiredService<ITenantContext>(),
            scope.ServiceProvider.GetRequiredService<ITenantLookup>());

        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor { EndpointMetadata = [new RequiresModuleAttribute(module)] });
        var executingContext = new ActionExecutingContext(
            actionContext, [], new Dictionary<string, object?>(), controller: new object());

        var invoked = false;
        await filter.OnActionExecutionAsync(executingContext, () =>
        {
            invoked = true;
            return Task.FromResult(new ActionExecutedContext(
                actionContext, [], controller: new object()));
        });

        return invoked ? null : (IActionResult?)executingContext.Result;
    }
}
