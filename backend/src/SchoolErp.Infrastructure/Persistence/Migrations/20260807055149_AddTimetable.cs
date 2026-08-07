using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "timetable_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: true),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    period = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_timetable_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_timetable_entries_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_timetable_entries_subject_id",
                table: "timetable_entries",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_timetable_entries_tenant_id",
                table: "timetable_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_timetable_entries_tenant_id_school_class_id_day_of_week",
                table: "timetable_entries",
                columns: new[] { "tenant_id", "school_class_id", "day_of_week" });

            migrationBuilder.EnableTenantRls("timetable_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("timetable_entries");

            migrationBuilder.DropTable(
                name: "timetable_entries");
        }
    }
}
