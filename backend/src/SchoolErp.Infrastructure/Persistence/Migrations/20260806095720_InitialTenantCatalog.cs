using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenantCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    subdomain = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    custom_domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                    address_line1 = table.Column<string>(type: "text", nullable: true),
                    address_line2 = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: true),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    contact_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    affiliation_board = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    affiliation_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    theme_primary_color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    theme_secondary_color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    plan = table.Column<int>(type: "integer", nullable: false),
                    subscription_expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    enabled_modules = table.Column<long>(type: "bigint", nullable: false),
                    storage_limit_mb = table.Column<int>(type: "integer", nullable: false),
                    sms_credits = table.Column<int>(type: "integer", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    default_language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenants_code",
                table: "tenants",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_custom_domain",
                table: "tenants",
                column: "custom_domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_subdomain",
                table: "tenants",
                column: "subdomain",
                unique: true);

            // Helper used by every row-level-security policy: returns the tenant
            // bound to the session by RlsSessionInterceptor, or NULL when no
            // tenant is bound (in which case policies match zero rows).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION app_current_tenant_id() RETURNS uuid AS $$
                    SELECT NULLIF(current_setting('app.tenant_id', true), '')::uuid
                $$ LANGUAGE sql STABLE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS app_current_tenant_id();");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
