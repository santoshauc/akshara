using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExaminations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_exams", x => x.id);
                    table.ForeignKey(
                        name: "fk_exams_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
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
                    table.PrimaryKey("pk_subjects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exam_subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_date = table.Column<DateOnly>(type: "date", nullable: true),
                    max_marks = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    pass_marks = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
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
                    table.PrimaryKey("pk_exam_subjects", x => x.id);
                    table.ForeignKey(
                        name: "fk_exam_subjects_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_subjects_school_classes_school_class_id",
                        column: x => x.school_class_id,
                        principalTable: "school_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_subjects_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mark_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    marks_obtained = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    is_absent = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_mark_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_mark_entries_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mark_entries_exam_subjects_exam_subject_id",
                        column: x => x.exam_subject_id,
                        principalTable: "exam_subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exam_subjects_exam_id",
                table: "exam_subjects",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_subjects_school_class_id",
                table: "exam_subjects",
                column: "school_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_subjects_subject_id",
                table: "exam_subjects",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_subjects_tenant_id",
                table: "exam_subjects",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_subjects_tenant_id_exam_id_school_class_id_subject_id",
                table: "exam_subjects",
                columns: new[] { "tenant_id", "exam_id", "school_class_id", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exams_academic_year_id",
                table: "exams",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_exams_tenant_id",
                table: "exams",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_exams_tenant_id_academic_year_id_name",
                table: "exams",
                columns: new[] { "tenant_id", "academic_year_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mark_entries_enrollment_id",
                table: "mark_entries",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_mark_entries_exam_subject_id",
                table: "mark_entries",
                column: "exam_subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_mark_entries_tenant_id",
                table: "mark_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_mark_entries_tenant_id_exam_subject_id_enrollment_id",
                table: "mark_entries",
                columns: new[] { "tenant_id", "exam_subject_id", "enrollment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mark_entries_tenant_id_student_id",
                table: "mark_entries",
                columns: new[] { "tenant_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "ix_subjects_tenant_id",
                table: "subjects",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_subjects_tenant_id_code",
                table: "subjects",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subjects_tenant_id_name",
                table: "subjects",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            // Second tenant-isolation layer for every exams table.
            migrationBuilder.EnableTenantRls("subjects");
            migrationBuilder.EnableTenantRls("exams");
            migrationBuilder.EnableTenantRls("exam_subjects");
            migrationBuilder.EnableTenantRls("mark_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("mark_entries");
            migrationBuilder.DisableTenantRls("exam_subjects");
            migrationBuilder.DisableTenantRls("exams");
            migrationBuilder.DisableTenantRls("subjects");

            migrationBuilder.DropTable(
                name: "mark_entries");

            migrationBuilder.DropTable(
                name: "exam_subjects");

            migrationBuilder.DropTable(
                name: "exams");

            migrationBuilder.DropTable(
                name: "subjects");
        }
    }
}
