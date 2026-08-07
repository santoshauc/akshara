using Microsoft.EntityFrameworkCore.Migrations;

namespace SchoolErp.Infrastructure.Persistence.Migrations;

/// <summary>
/// Row-level-security helpers for migrations. Every migration that creates a
/// tenant-scoped table MUST call <see cref="EnableTenantRls"/> for it — this is
/// the second enforcement layer behind the EF global query filters.
/// </summary>
public static class RlsMigrationExtensions
{
    /// <summary>
    /// Enables RLS on <paramref name="table"/> and installs a policy that only
    /// exposes rows whose <c>tenant_id</c> matches the session tenant set by
    /// <c>app_current_tenant_id()</c>. <c>FORCE</c> ensures the policy also
    /// applies to the table owner (the application role).
    /// </summary>
    public static void EnableTenantRls(this MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.Sql($"""
            ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation_{table} ON "{table}"
                USING (tenant_id = app_current_tenant_id())
                WITH CHECK (tenant_id = app_current_tenant_id());
            """);
    }

    /// <summary>Reverts <see cref="EnableTenantRls"/> (for Down migrations).</summary>
    public static void DisableTenantRls(this MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.Sql($"""
            DROP POLICY IF EXISTS tenant_isolation_{table} ON "{table}";
            ALTER TABLE "{table}" NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE "{table}" DISABLE ROW LEVEL SECURITY;
            """);
    }
}
