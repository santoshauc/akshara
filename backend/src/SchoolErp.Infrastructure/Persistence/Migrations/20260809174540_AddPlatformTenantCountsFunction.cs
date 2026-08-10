using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lets the platform count what its schools contain without breaking
    /// tenant isolation.
    ///
    /// Every people table is RLS'd with FORCE, so the policy binds the
    /// application role too and a cross-tenant COUNT returns zero rows — not
    /// an error, which is the dangerous part. The alternative to this function
    /// is looping the whole catalog and re-issuing every query per tenant with
    /// app.tenant_id switched, which is N round trips for a page that loads on
    /// every Super Admin sign-in.
    ///
    /// SECURITY DEFINER runs it as the table owner, so RLS is bypassed. It is
    /// deliberately narrow: it returns COUNTS GROUPED BY TENANT and nothing
    /// else, so no row, name or identifier can escape through it. EXECUTE is
    /// revoked from PUBLIC and granted only to the runtime role; the endpoint
    /// on top additionally demands a platform principal.
    /// </summary>
    public partial class AddPlatformTenantCountsFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // search_path is pinned: a SECURITY DEFINER function that resolves
            // object names through the caller's search_path is a privilege
            // escalation waiting to happen.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION app_platform_tenant_counts()
                RETURNS TABLE (
                    tenant_id uuid,
                    active_students bigint,
                    active_teachers bigint,
                    guardians bigint,
                    open_campuses bigint)
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = public
                AS $$
                    SELECT t.id,
                           (SELECT count(*) FROM students s
                             WHERE s.tenant_id = t.id AND NOT s.is_deleted AND s.status = 1),
                           (SELECT count(*) FROM teachers te
                             WHERE te.tenant_id = t.id AND NOT te.is_deleted AND te.is_active),
                           (SELECT count(*) FROM guardians g
                             WHERE g.tenant_id = t.id AND NOT g.is_deleted),
                           (SELECT count(*) FROM campuses c
                             WHERE c.tenant_id = t.id AND NOT c.is_deleted AND c.is_active)
                    FROM tenants t
                    WHERE NOT t.is_deleted;
                $$;

                REVOKE ALL ON FUNCTION app_platform_tenant_counts() FROM PUBLIC;
                """);

            // The runtime role differs by environment (schoolerp_app locally);
            // granting to whoever the app connects as keeps this portable
            // without hardcoding a role name that may not exist.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'schoolerp_app') THEN
                        GRANT EXECUTE ON FUNCTION app_platform_tenant_counts() TO schoolerp_app;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS app_platform_tenant_counts();");
    }
}
