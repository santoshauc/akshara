using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Identity;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.IntegrationTests.Tenancy;
using SchoolErp.Shared.Authorization;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Auth;

/// <summary>
/// Boots the real Infrastructure composition (EF Core, Identity, tenancy,
/// auth services) against a disposable PostgreSQL container, and seeds one
/// school with staff and parent accounts.
/// </summary>
public sealed class AuthTestFixture : IAsyncLifetime
{
    public const string SchoolCode = "DEMO01";
    public const string AdminEmail = "admin@demo.school";
    public const string AdminPassword = "Admin@12345";
    public const string LockoutEmail = "lockme@demo.school";
    public const string ParentPhone = "+911111111111";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_auth_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    /// <summary>Captures outbound SMS so tests can read OTP codes.</summary>
    public RecordingSmsSender SmsSender { get; } = new();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _container.GetConnectionString(),
                ["Jwt:Issuer"] = "SchoolErp.Tests",
                ["Jwt:Audience"] = "SchoolErp.Tests",
                ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789abcdef",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser, StubCurrentUser>();
        services.AddSingleton<ISmsSender>(SmsSender); // replaces DevSmsSender
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await SeedAsync(scope.ServiceProvider, db);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>Creates a fresh DI scope; resolve services per test from it.</summary>
    public AsyncServiceScope CreateScope() => _provider.CreateAsyncScope();

    private async Task SeedAsync(IServiceProvider services, AppDbContext db)
    {
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Code = SchoolCode,
            Name = "Demo Public School",
            Subdomain = "demo",
            Status = TenantStatus.Active,
            SmsCredits = 1_000,
        });

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = "SchoolAdmin",
            NormalizedName = "SCHOOLADMIN",
            TenantId = TenantId,
        };
        db.Roles.Add(role);
        db.RoleClaims.Add(new IdentityRoleClaim<Guid>
        {
            RoleId = role.Id,
            ClaimType = Permissions.ClaimType,
            ClaimValue = Permissions.Users.View,
        });
        await db.SaveChangesAsync();

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var admin = NewUser(AdminEmail, "+919876543210", "Demo Admin");
        (await userManager.CreateAsync(admin, AdminPassword)).Succeeded
            .Should().BeTrue("seeding the admin user must succeed");
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = admin.Id, RoleId = role.Id });

        var lockout = NewUser(LockoutEmail, "+919876543211", "Lockout Target");
        (await userManager.CreateAsync(lockout, AdminPassword)).Succeeded.Should().BeTrue();

        var parent = NewUser("parent@demo.school", ParentPhone, "Demo Parent");
        (await userManager.CreateAsync(parent, AdminPassword)).Succeeded.Should().BeTrue();

        await db.SaveChangesAsync();
    }

    private ApplicationUser NewUser(string email, string phone, string name) => new()
    {
        Id = Guid.NewGuid(),
        UserName = Guid.NewGuid().ToString("N"),
        Email = email,
        PhoneNumber = phone,
        FullName = name,
        TenantId = TenantId,
        EmailConfirmed = true,
        PhoneNumberConfirmed = true,
    };
}

/// <summary>Records outbound SMS messages instead of sending them.</summary>
public sealed class RecordingSmsSender : ISmsSender
{
    private readonly List<(string Phone, string Message)> _sent = [];

    public IReadOnlyList<(string Phone, string Message)> Sent => _sent;

    public Task SendAsync(string phone, string message, CancellationToken ct = default)
    {
        _sent.Add((phone, message));
        return Task.CompletedTask;
    }

    /// <summary>Extracts the 6-digit code from the most recent message.</summary>
    public string LastCode() =>
        new(Sent[^1].Message.TakeWhile(char.IsDigit).ToArray());
}
