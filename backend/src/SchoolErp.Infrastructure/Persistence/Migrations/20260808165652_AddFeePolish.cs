using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeePolish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "late_fine_type",
                table: "fee_heads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "late_fine_value",
                table: "fee_heads",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "fee_concessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fee_head_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
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
                    table.PrimaryKey("pk_fee_concessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_fee_concessions_fee_heads_fee_head_id",
                        column: x => x.fee_head_id,
                        principalTable: "fee_heads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fee_concessions_fee_head_id",
                table: "fee_concessions",
                column: "fee_head_id");

            migrationBuilder.CreateIndex(
                name: "ix_fee_concessions_tenant_id",
                table: "fee_concessions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_fee_concessions_tenant_id_student_id_academic_year_id",
                table: "fee_concessions",
                columns: new[] { "tenant_id", "student_id", "academic_year_id" });

            migrationBuilder.EnableTenantRls("fee_concessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("fee_concessions");
            migrationBuilder.DropTable(
                name: "fee_concessions");

            migrationBuilder.DropColumn(
                name: "late_fine_type",
                table: "fee_heads");

            migrationBuilder.DropColumn(
                name: "late_fine_value",
                table: "fee_heads");
        }
    }
}
