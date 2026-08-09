using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableSubstitutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "timetable_substitutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    timetable_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    absent_teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    substitute_teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_timetable_substitutions", x => x.id);
                    table.ForeignKey(
                        name: "fk_timetable_substitutions_teachers_substitute_teacher_id",
                        column: x => x.substitute_teacher_id,
                        principalTable: "teachers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_timetable_substitutions_timetable_entries_timetable_entry_id",
                        column: x => x.timetable_entry_id,
                        principalTable: "timetable_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_timetable_substitutions_substitute_teacher_id",
                table: "timetable_substitutions",
                column: "substitute_teacher_id");

            migrationBuilder.CreateIndex(
                name: "ix_timetable_substitutions_tenant_id",
                table: "timetable_substitutions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_timetable_substitutions_tenant_id_date_timetable_entry_id",
                table: "timetable_substitutions",
                columns: new[] { "tenant_id", "date", "timetable_entry_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_timetable_substitutions_timetable_entry_id",
                table: "timetable_substitutions",
                column: "timetable_entry_id");

            migrationBuilder.EnableTenantRls("timetable_substitutions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("timetable_substitutions");
            migrationBuilder.DropTable(
                name: "timetable_substitutions");
        }
    }
}
