using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFrontOffice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gate_passes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pass_number = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    released_to = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    released_to_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_gate_passes", x => x.id);
                    table.ForeignKey(
                        name: "fk_gate_passes_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "visitor_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    visitor_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    purpose = table.Column<int>(type: "integer", nullable: false),
                    whom_to_meet = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pass_number = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    checked_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    checked_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    remarks = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("pk_visitor_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_visitor_entries_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gate_passes_student_id",
                table: "gate_passes",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_gate_passes_tenant_id",
                table: "gate_passes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_gate_passes_tenant_id_issued_at",
                table: "gate_passes",
                columns: new[] { "tenant_id", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_gate_passes_tenant_id_pass_number",
                table: "gate_passes",
                columns: new[] { "tenant_id", "pass_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_visitor_entries_student_id",
                table: "visitor_entries",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_visitor_entries_tenant_id",
                table: "visitor_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_visitor_entries_tenant_id_checked_in_at",
                table: "visitor_entries",
                columns: new[] { "tenant_id", "checked_in_at" });

            migrationBuilder.CreateIndex(
                name: "ix_visitor_entries_tenant_id_checked_out_at",
                table: "visitor_entries",
                columns: new[] { "tenant_id", "checked_out_at" });

            migrationBuilder.EnableTenantRls("visitor_entries");
            migrationBuilder.EnableTenantRls("gate_passes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gate_passes");

            migrationBuilder.DropTable(
                name: "visitor_entries");
        }
    }
}
