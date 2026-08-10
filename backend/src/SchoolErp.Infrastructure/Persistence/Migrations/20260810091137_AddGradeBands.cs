using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeBands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grade_bands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    letter = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    point = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_grade_bands", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_grade_bands_tenant_id",
                table: "grade_bands",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_grade_bands_tenant_id_min_percent",
                table: "grade_bands",
                columns: new[] { "tenant_id", "min_percent" },
                unique: true);

            // Tenant-scoped table, so the second enforcement layer is required.
            migrationBuilder.EnableTenantRls("grade_bands");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("grade_bands");

            migrationBuilder.DropTable(
                name: "grade_bands");
        }
    }
}
