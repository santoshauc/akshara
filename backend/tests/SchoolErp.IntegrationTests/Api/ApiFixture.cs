using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Identity;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.IntegrationTests.Auth;
using SchoolErp.IntegrationTests.Tenancy;
using SchoolErp.Shared.Authorization;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Api;

/// <summary>Which signed-in person a test wants to be.</summary>
public enum TestPrincipal
{
    /// <summary>A school administrator holding every tenant-assignable permission.</summary>
    SchoolAdmin,

    /// <summary>Staff whose roles grant classroom permissions only — no students.view.</summary>
    LimitedStaff,

    /// <summary>
    /// A school admin who has somehow acquired tenants.view. Exists to prove the
    /// platform policy refuses school tokens on the PERMISSION alone.
    /// </summary>
    SchoolAdminWithPlatformPermission,

    /// <summary>A parent with no children at this school.</summary>
    Parent,

    /// <summary>A platform operator with MFA enabled — the only kind that works.</summary>
    PlatformOperator,

    /// <summary>A platform operator who has not enrolled MFA yet.</summary>
    PlatformOperatorWithoutMfa,
}

/// <summary>
/// Boots the REAL API over a disposable PostgreSQL container and issues requests
/// through it.
///
/// Every other integration fixture in this repo calls handlers directly. That
/// leaves the whole API layer — routing, JWT validation, the permission policies,
/// the tenant guard, the module gate, the security headers, the shape a problem
/// response actually has on the wire — measured at 20% and asserted nowhere. This
/// fixture exists so those can be tested as a wired pipeline rather than as parts.
///
/// The database is arranged exactly as production is: migrations and Hangfire use
/// the OWNER connection, while the API itself connects as a restricted role,
/// because PostgreSQL superusers bypass row-level security and a superuser API
/// would make RLS silently inert.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime, IDisposable
{
    /// <summary>The seeded school. Short and distinctive so it cannot collide.</summary>
    public const string SchoolCode = "HTTP01";

    public const string SchoolAdminEmail = "admin@http.test";
    public const string SchoolAdminPassword = "Http@12345";

    /// <summary>A phone the school really does know, for the OTP tests.</summary>
    public const string ParentPhone = "+919000000004";

    private const string AppRole = "api_test_app";
    private const string AppRolePassword = "api_test_app_pw";

    /// <summary>The one origin CORS is configured to allow, so a test can prove
    /// that an allowed origin is echoed and an unknown one is not.</summary>
    public const string AllowedOrigin = "http://localhost:5050";

    /// <summary>
    /// Deliberately unreachable. The readiness probe must go unhealthy when a
    /// downstream service is down while liveness stays up — that split is the
    /// whole point of having two probes, and a dead port is how a test proves it.
    /// abortConnect keeps the multiplexer from throwing on construction and the
    /// short timeout keeps the probe from stalling the test.
    /// </summary>
    private const string DeadRedis = "localhost:59999,abortConnect=false,connectTimeout=250,connectRetry=0";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_api_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private readonly Dictionary<TestPrincipal, string> _tokens = [];
    private readonly Dictionary<string, string?> _originalEnvironment = [];

    private ApiFactory _factory = null!;
    private string _adminConnectionString = string.Empty;
    private string _appConnectionString = string.Empty;

    /// <summary>The seeded school's id.</summary>
    public Guid TenantId { get; } = Guid.NewGuid();

    /// <summary>Captures outbound SMS so an OTP code can be read back.</summary>
    public RecordingSmsSender SmsSender { get; } = new();

    /// <summary>The running host's services, for tests that need to look at state directly.</summary>
    public IServiceProvider Services => _factory.Services;

    /// <summary>
    /// A student id that belongs to nobody. Parent endpoints must answer 404 for
    /// it rather than 403 — telling a stranger "forbidden" confirms the child
    /// exists, so the family guard deliberately denies knowledge instead.
    /// </summary>
    public Guid UnrelatedStudentId { get; } = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _adminConnectionString = _container.GetConnectionString();
        _appConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Username = AppRole,
            Password = AppRolePassword,
        }.ConnectionString;

        var seededUsers = await MigrateAndSeedAsync();

        ApplyHostConfigurationEnvironment();
        _factory = new ApiFactory(_appConnectionString, _adminConnectionString, DeadRedis, SmsSender);

        // Touching Services builds the host, so a startup failure surfaces here
        // rather than inside whichever test happened to run first.
        GuardAgainstTouchingTheDevDatabase(_factory.Services);

        // Tokens are MINTED rather than obtained by logging in. The auth
        // endpoints share one fixed-window limiter of 10 requests a minute
        // across the whole app instance, so signing six principals in would
        // spend most of that budget before the first test ran, and any test
        // that later touched /auth would fail on an unrelated 429. Real login
        // over HTTP is covered by AuthEndpointTests, which owns its own
        // instance and therefore its own budget.
        var tokenService = _factory.Services.GetRequiredService<JwtTokenService>();
        foreach (var (principal, user) in seededUsers)
        {
            _tokens[principal] = tokenService.CreateAccessToken(
                user, roles: [], permissions: PermissionsFor(principal), schoolCode: SchoolCode);
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _container.DisposeAsync();
        RestoreHostConfigurationEnvironment();
    }

    /// <summary>
    /// xUnit disposes the fixture through <see cref="IAsyncLifetime"/>; this
    /// exists so the analyzer can see that the host is owned and released.
    /// Both paths are safe to run — the factory tolerates repeat disposal.
    /// </summary>
    public void Dispose() => _factory?.Dispose();

    /// <summary>A client carrying no credentials at all.</summary>
    public HttpClient CreateAnonymousClient() =>
        // Redirects are left unfollowed so a test sees the redirect itself. The
        // HTTPS redirection middleware is in this pipeline, and following its
        // 307 would send the request at a port nothing is listening on.
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>A client signed in as <paramref name="principal"/>.</summary>
    public HttpClient CreateClient(TestPrincipal principal)
    {
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokens[principal]);
        return client;
    }

    /// <summary>
    /// A second, independent app instance. Rate limiter state lives in the app,
    /// so a test that deliberately exhausts a limiter has to own its own host or
    /// it takes every other test down with it.
    /// </summary>
    public ApiFactory CreateIsolatedHost() =>
        new(_appConnectionString, _adminConnectionString, DeadRedis, new RecordingSmsSender());

    /// <summary>
    /// The permission bundle each principal's roles would grant. Authorization
    /// reads the JWT, never the database, so this list IS the principal's
    /// authority — which is exactly what makes it worth varying per test.
    /// </summary>
    private static string[] PermissionsFor(TestPrincipal principal) => principal switch
    {
        TestPrincipal.SchoolAdmin => [.. Permissions.TenantAssignable],
        TestPrincipal.LimitedStaff => [Permissions.Attendance.View, Permissions.Attendance.Mark],
        // A school token carrying the platform permission. If the platform
        // endpoints ever check the permission instead of the policy again, this
        // principal walks straight into the tenant catalog.
        TestPrincipal.SchoolAdminWithPlatformPermission =>
            [.. Permissions.TenantAssignable, Permissions.TenantCatalog.View, Permissions.TenantCatalog.Manage],
        TestPrincipal.Parent => [],
        TestPrincipal.PlatformOperator or TestPrincipal.PlatformOperatorWithoutMfa =>
            [.. Permissions.All],
        _ => [],
    };

    /// <summary>
    /// Applies migrations as the owner, creates the restricted runtime role, and
    /// seeds the school and its people. Seeding runs on the OWNER connection like
    /// every other fixture here: RLS is FORCEd, so seeding a platform user (who
    /// has no tenant at all) through the restricted role would be refused by the
    /// policy rather than by any rule the product actually has.
    /// </summary>
    private async Task<Dictionary<TestPrincipal, ApplicationUser>> MigrateAndSeedAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _adminConnectionString,
                ["Jwt:Issuer"] = ApiFactory.Issuer,
                ["Jwt:Audience"] = ApiFactory.Audience,
                ["Jwt:SigningKey"] = ApiFactory.SigningKey,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser, StubCurrentUser>();

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        await db.Database.ExecuteSqlRawAsync($"""
            CREATE ROLE {AppRole} LOGIN PASSWORD '{AppRolePassword}' NOSUPERUSER;
            GRANT USAGE ON SCHEMA public TO {AppRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {AppRole};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {AppRole};
            """);

        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Code = SchoolCode,
            Name = "HTTP Test School",
            Subdomain = "httptest",
            Status = TenantStatus.Active,
            SmsCredits = 1_000,
            // Library is deliberately OFF while Examination is on, so one tenant
            // demonstrates the module gate both refusing and allowing.
            EnabledModules = TenantModules.Core | TenantModules.Examination | TenantModules.Fees,
            SubscriptionExpiresOn = new DateOnly(2099, 1, 1),
            ThemePrimaryColor = "#00695C",
        });
        await db.SaveChangesAsync();

        // A real SchoolAdmin role with real permission claims. Minted tokens
        // carry whatever permissions a test asks for, so without this the suite
        // would never notice that a token obtained by ACTUALLY LOGGING IN comes
        // out empty — which is exactly what happened: the login round-trip test
        // got a valid token and a 403 behind it.
        var adminRole = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = "SchoolAdmin",
            NormalizedName = "SCHOOLADMIN",
            TenantId = TenantId,
        };
        db.Roles.Add(adminRole);
        foreach (var permission in Permissions.TenantAssignable)
        {
            db.RoleClaims.Add(new IdentityRoleClaim<Guid>
            {
                RoleId = adminRole.Id,
                ClaimType = Permissions.ClaimType,
                ClaimValue = permission,
            });
        }

        await db.SaveChangesAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var users = new Dictionary<TestPrincipal, ApplicationUser>();

        users[TestPrincipal.SchoolAdmin] = await CreateUserAsync(
            userManager, SchoolAdminEmail, "+919000000001", "HTTP Admin", TenantId);
        db.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = users[TestPrincipal.SchoolAdmin].Id,
            RoleId = adminRole.Id,
        });
        await db.SaveChangesAsync();
        users[TestPrincipal.LimitedStaff] = await CreateUserAsync(
            userManager, "limited@http.test", "+919000000002", "HTTP Limited", TenantId);
        users[TestPrincipal.SchoolAdminWithPlatformPermission] = await CreateUserAsync(
            userManager, "escalated@http.test", "+919000000003", "HTTP Escalated", TenantId);
        users[TestPrincipal.Parent] = await CreateUserAsync(
            userManager, "parent@http.test", ParentPhone, "HTTP Parent", TenantId);

        // Platform accounts have no tenant. MFA is the difference between the two:
        // the token service stamps a "setup required" claim on an operator who has
        // not enrolled, and the platform policy refuses anything carrying it.
        var operatorWithMfa = await CreateUserAsync(
            userManager, "super@http.test", "+919000000005", "HTTP Operator", tenantId: null);
        operatorWithMfa.TwoFactorEnabled = true;
        await userManager.UpdateAsync(operatorWithMfa);
        users[TestPrincipal.PlatformOperator] = operatorWithMfa;

        users[TestPrincipal.PlatformOperatorWithoutMfa] = await CreateUserAsync(
            userManager, "super-nomfa@http.test", "+919000000006", "HTTP Operator No MFA", tenantId: null);

        return users;
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string phone,
        string name,
        Guid? tenantId)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = Guid.NewGuid().ToString("N"),
            Email = email,
            PhoneNumber = phone,
            FullName = name,
            TenantId = tenantId,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, SchoolAdminPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Seeding {email} failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    /// <summary>
    /// Configuration the API reads EAGERLY, while its services are being
    /// registered, has to arrive as environment variables.
    ///
    /// This is not a stylistic choice. AddInfrastructure reads
    /// ConnectionStrings:Postgres at registration time, and a
    /// WebApplicationFactory applies ConfigureAppConfiguration later than that —
    /// so an in-memory override lands after the value has already been captured
    /// and is simply ignored. It fails silently and in the worst possible
    /// direction: the host quietly used the developer's REAL database at
    /// localhost:5432. Environment variables work because CreateBuilder adds
    /// them as a source after the appsettings files, so they outrank
    /// appsettings.json and are in place before any of this code runs.
    ///
    /// Contained on purpose: every other fixture in this assembly builds its own
    /// ConfigurationBuilder with no environment-variable source, so none of them
    /// can see these.
    /// </summary>
    private void ApplyHostConfigurationEnvironment()
    {
        foreach (var (key, value) in new Dictionary<string, string?>
        {
            ["ConnectionStrings__Postgres"] = _appConnectionString,
            ["ConnectionStrings__PostgresMigrations"] = _adminConnectionString,
            ["ConnectionStrings__Redis"] = DeadRedis,
            ["Jwt__Issuer"] = ApiFactory.Issuer,
            ["Jwt__Audience"] = ApiFactory.Audience,
            ["Jwt__SigningKey"] = ApiFactory.SigningKey,
            ["Jwt__AccessTokenMinutes"] = "15",
            ["Jwt__RefreshTokenDays"] = "7",
            ["Jwt__RequirePlatformMfa"] = "true",
            ["Cors__AllowedOrigins__0"] = AllowedOrigin,
        })
        {
            _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private void RestoreHostConfigurationEnvironment()
    {
        foreach (var (key, value) in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        _originalEnvironment.Clear();
    }

    /// <summary>
    /// The API's own appsettings.json points at a REAL local development
    /// database. This asks the DbContext what it is ACTUALLY connected to,
    /// rather than asking configuration what it was told — an earlier version
    /// checked configuration, passed, and the host was talking to the developer's
    /// working data the whole time. Cheap assertion, expensive mistake.
    /// </summary>
    private void GuardAgainstTouchingTheDevDatabase(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var effective = scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Database.GetConnectionString();

        if (effective != _appConnectionString)
        {
            throw new InvalidOperationException(
                "The API test host is NOT connected to the throwaway container. It is using " +
                $"'{effective}'. Refusing to run tests against it.");
        }
    }
}

/// <summary>
/// The API host itself, pointed at a container and stripped of the things a test
/// must not run: the Hangfire worker, and anything that reaches off the machine.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    internal const string Issuer = "SchoolErp.ApiTests";
    internal const string Audience = "SchoolErp.ApiTests";
    internal const string SigningKey = "api-http-test-signing-key-0123456789abcdef";

    private readonly string _appConnectionString;
    private readonly string _ownerConnectionString;
    private readonly string _redisConnectionString;
    private readonly ISmsSender _smsSender;

    internal ApiFactory(
        string appConnectionString,
        string ownerConnectionString,
        string redisConnectionString,
        ISmsSender smsSender)
    {
        _appConnectionString = appConnectionString;
        _ownerConnectionString = ownerConnectionString;
        _redisConnectionString = redisConnectionString;
        _smsSender = smsSender;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // NOT Development. That environment mounts Swagger, the Hangfire
        // dashboard and — the one that matters — runs DevSeeder and the whole
        // demo dataset at startup, which would take longer than the tests and
        // bury the seeded school in noise.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                // Added last, so it outranks appsettings.json.
                ["ConnectionStrings:Postgres"] = _appConnectionString,
                // Migrations and Hangfire storage run as the owner in production
                // too — the restricted role cannot CREATE SCHEMA.
                ["ConnectionStrings:PostgresMigrations"] = _ownerConnectionString,
                ["ConnectionStrings:Redis"] = _redisConnectionString,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                // Left at the production default ON PURPOSE: a platform operator
                // without MFA must still be refused, and one of these tests
                // proves it. appsettings.Development.json turns this off, but
                // that file is not loaded outside Development.
                ["Jwt:RequirePlatformMfa"] = "true",
                ["Cors:AllowedOrigins:0"] = ApiFixture.AllowedOrigin,
                ["Sms:Provider"] = "dev",
                ["WhatsApp:Provider"] = "dev",
                // Leaving this unset keeps OpenTelemetry dormant, so the tests
                // do not try to reach a collector that is not there.
                ["Otlp:Endpoint"] = string.Empty,
            }));

        builder.ConfigureTestServices(services =>
        {
            // Hangfire's worker would run the outbox dispatcher every 15 seconds
            // against the test database, mutating outbox rows underneath any
            // assertion about them. Storage stays registered — Program.cs
            // resolves IRecurringJobManager at startup and would fail without it.
            foreach (var descriptor in services
                .Where(d => d.ServiceType == typeof(IHostedService) &&
                            d.ImplementationType?.FullName?.StartsWith("Hangfire", StringComparison.Ordinal) == true)
                .ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton(_smsSender);
        });
    }
}
