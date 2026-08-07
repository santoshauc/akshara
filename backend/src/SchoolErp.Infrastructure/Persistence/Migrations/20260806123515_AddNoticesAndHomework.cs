using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNoticesAndHomework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "homework_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    assigned_on = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("pk_homework_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_homework_assignments_school_classes_school_class_id",
                        column: x => x.school_class_id,
                        principalTable: "school_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_homework_assignments_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    school_class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_pinned = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_notices", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_homework_assignments_school_class_id",
                table: "homework_assignments",
                column: "school_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_homework_assignments_subject_id",
                table: "homework_assignments",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_homework_assignments_tenant_id",
                table: "homework_assignments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_homework_assignments_tenant_id_school_class_id_due_date",
                table: "homework_assignments",
                columns: new[] { "tenant_id", "school_class_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_notices_tenant_id",
                table: "notices",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notices_tenant_id_created_at",
                table: "notices",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.EnableTenantRls("notices");
            migrationBuilder.EnableTenantRls("homework_assignments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("homework_assignments");
            migrationBuilder.DisableTenantRls("notices");

            migrationBuilder.DropTable(
                name: "homework_assignments");

            migrationBuilder.DropTable(
                name: "notices");
        }
    }
}
