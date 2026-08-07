using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeachers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "teacher_id",
                table: "timetable_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "teachers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    full_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    qualification = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    specialization = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    joined_on = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_teachers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_timetable_entries_teacher_id",
                table: "timetable_entries",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "ix_teachers_tenant_id",
                table: "teachers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_teachers_tenant_id_employee_code",
                table: "teachers",
                columns: new[] { "tenant_id", "employee_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teachers_tenant_id_phone",
                table: "teachers",
                columns: new[] { "tenant_id", "phone" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_timetable_entries_teachers_teacher_id",
                table: "timetable_entries",
                column: "teacher_id",
                principalTable: "teachers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.EnableTenantRls("teachers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("teachers");

            migrationBuilder.DropForeignKey(
                name: "fk_timetable_entries_teachers_teacher_id",
                table: "timetable_entries");

            migrationBuilder.DropTable(
                name: "teachers");

            migrationBuilder.DropIndex(
                name: "ix_timetable_entries_teacher_id",
                table: "timetable_entries");

            migrationBuilder.DropColumn(
                name: "teacher_id",
                table: "timetable_entries");
        }
    }
}
