using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Persistence.Interceptors;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Tenancy;

/// <summary>
/// Spins up a disposable PostgreSQL 16 container, applies the real migrations
/// (as the superuser), and creates a restricted <c>app_user</c> role for test
/// connections — mirroring production, where the API never connects as a
/// superuser because superusers bypass row-level security.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string AppRole = "app_user";
    private const string AppRolePassword = "app_user_test_pw";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    /// <summary>Connection string for the restricted application role.</summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    /// <summary>Connection string for the superuser (migrations, verification).</summary>
    public string AdminConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        AdminConnectionString = _container.GetConnectionString();
        AppConnectionString = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Username = AppRole,
            Password = AppRolePassword,
        }.ConnectionString;

        // Apply the real migrations — including RLS policies — as the owner.
        await using (var adminContext = CreateContext(AdminConnectionString, new StubTenantContext()))
        {
            await adminContext.Database.MigrateAsync();

            await adminContext.Database.ExecuteSqlRawAsync($"""
                CREATE ROLE {AppRole} LOGIN PASSWORD '{AppRolePassword}' NOSUPERUSER;
                GRANT USAGE ON SCHEMA public TO {AppRole};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {AppRole};
                """);
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Context connected as the restricted app role, bound to a tenant
    /// (or unbound when <paramref name="tenantId"/> is null).
    /// </summary>
    public AppDbContext CreateAppContext(Guid? tenantId) =>
        CreateContext(AppConnectionString, new StubTenantContext(tenantId));

    /// <summary>Superuser context for seeding the tenant catalog and verification.</summary>
    public AppDbContext CreateAdminContext(Guid? tenantId = null) =>
        CreateContext(AdminConnectionString, new StubTenantContext(tenantId));

    private static AppDbContext CreateContext(string connectionString, StubTenantContext tenantContext)
    {
        // Mirrors the production configuration in DependencyInjection.AddInfrastructure.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new AuditableEntityInterceptor(new StubCurrentUser(), tenantContext, TimeProvider.System),
                new RlsSessionInterceptor(tenantContext))
            .Options;

        return new AppDbContext(options, tenantContext);
    }
}
