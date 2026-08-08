using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTermReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "term_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
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
                    table.PrimaryKey("pk_term_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "term_student_inputs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    co_scholastic_json = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    remarks = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("pk_term_student_inputs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "term_report_components",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weight_percent = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("pk_term_report_components", x => x.id);
                    table.ForeignKey(
                        name: "fk_term_report_components_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_term_report_components_term_reports_term_report_id",
                        column: x => x.term_report_id,
                        principalTable: "term_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_term_report_components_exam_id",
                table: "term_report_components",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "ix_term_report_components_tenant_id",
                table: "term_report_components",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_term_report_components_tenant_id_term_report_id_exam_id",
                table: "term_report_components",
                columns: new[] { "tenant_id", "term_report_id", "exam_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_term_report_components_term_report_id",
                table: "term_report_components",
                column: "term_report_id");

            migrationBuilder.CreateIndex(
                name: "ix_term_reports_tenant_id",
                table: "term_reports",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_term_reports_tenant_id_academic_year_id_name",
                table: "term_reports",
                columns: new[] { "tenant_id", "academic_year_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_term_student_inputs_tenant_id",
                table: "term_student_inputs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_term_student_inputs_tenant_id_term_report_id_student_id",
                table: "term_student_inputs",
                columns: new[] { "tenant_id", "term_report_id", "student_id" },
                unique: true);

            migrationBuilder.EnableTenantRls("term_reports");
            migrationBuilder.EnableTenantRls("term_report_components");
            migrationBuilder.EnableTenantRls("term_student_inputs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("term_student_inputs");
            migrationBuilder.DisableTenantRls("term_report_components");
            migrationBuilder.DisableTenantRls("term_reports");
            migrationBuilder.DropTable(
                name: "term_report_components");

            migrationBuilder.DropTable(
                name: "term_student_inputs");

            migrationBuilder.DropTable(
                name: "term_reports");
        }
    }
}
