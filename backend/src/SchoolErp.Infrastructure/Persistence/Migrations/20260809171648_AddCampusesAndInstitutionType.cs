using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampusesAndInstitutionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue 1 = InstitutionType.School, NOT the scaffolded 0,
            // which is not a member of the enum. The C# initialiser does not
            // touch existing rows, so every school already in the catalog would
            // otherwise read as an unknown institution type.
            migrationBuilder.AddColumn<int>(
                name: "institution_type",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "campuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    address_line1 = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    state = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campuses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_campuses_tenant_id",
                table: "campuses",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_campuses_tenant_id_code",
                table: "campuses",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_campuses_tenant_id_is_active",
                table: "campuses",
                columns: new[] { "tenant_id", "is_active" });

            // Every existing school already operates from somewhere, so give
            // each one a primary campus built from the address it already has.
            // Without this they would all report zero campuses, which is not
            // "no data" — it is wrong.
            //
            // This MUST run before RLS is enabled: the policy is FORCEd, so it
            // applies to the owner role the migration runs as, and the
            // WITH CHECK would reject every row (app.tenant_id is unset here).
            migrationBuilder.Sql(
                """
                INSERT INTO campuses (
                    id, tenant_id, name, code, address_line1, city, state,
                    postal_code, contact_phone, is_primary, is_active,
                    created_at, is_deleted)
                SELECT gen_random_uuid(), id, 'Main Campus', 'MAIN',
                       address_line1, city, state, postal_code, contact_phone,
                       true, true, now(), false
                FROM tenants
                WHERE is_deleted = false;
                """);

            // Second enforcement layer behind the EF query filters. Mandatory
            // for every tenant-scoped table.
            migrationBuilder.EnableTenantRls("campuses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("campuses");

            migrationBuilder.DropTable(
                name: "campuses");

            migrationBuilder.DropColumn(
                name: "institution_type",
                table: "tenants");
        }
    }
}
