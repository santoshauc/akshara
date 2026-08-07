using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentInformationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guardians",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    relation = table.Column<int>(type: "integer", nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    occupation = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_guardians", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "school_classes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_school_classes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admission_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    first_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    gender = table.Column<int>(type: "integer", nullable: false),
                    blood_group = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address_line1 = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    city = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    photo_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    medical_notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    admission_date = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("pk_students", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_sections", x => x.id);
                    table.ForeignKey(
                        name: "fk_sections_school_classes_school_class_id",
                        column: x => x.school_class_id,
                        principalTable: "school_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_guardians",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    guardian_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_student_guardians", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_guardians_guardians_guardian_id",
                        column: x => x.guardian_id,
                        principalTable: "guardians",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_guardians_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    roll_number = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_enrollments", x => x.id);
                    table.ForeignKey(
                        name: "fk_enrollments_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_school_classes_school_class_id",
                        column: x => x.school_class_id,
                        principalTable: "school_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_academic_year_id",
                table: "enrollments",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_school_class_id",
                table: "enrollments",
                column: "school_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_section_id",
                table: "enrollments",
                column: "section_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_student_id",
                table: "enrollments",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_tenant_id",
                table: "enrollments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_tenant_id_academic_year_id_school_class_id_sect",
                table: "enrollments",
                columns: new[] { "tenant_id", "academic_year_id", "school_class_id", "section_id" });

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_tenant_id_student_id_academic_year_id",
                table: "enrollments",
                columns: new[] { "tenant_id", "student_id", "academic_year_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_guardians_tenant_id",
                table: "guardians",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_guardians_tenant_id_phone",
                table: "guardians",
                columns: new[] { "tenant_id", "phone" });

            migrationBuilder.CreateIndex(
                name: "ix_school_classes_tenant_id",
                table: "school_classes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_school_classes_tenant_id_name",
                table: "school_classes",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sections_school_class_id",
                table: "sections",
                column: "school_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_sections_tenant_id",
                table: "sections",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sections_tenant_id_school_class_id_name",
                table: "sections",
                columns: new[] { "tenant_id", "school_class_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_guardians_guardian_id",
                table: "student_guardians",
                column: "guardian_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_guardians_student_id",
                table: "student_guardians",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_guardians_tenant_id",
                table: "student_guardians",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_guardians_tenant_id_student_id_guardian_id",
                table: "student_guardians",
                columns: new[] { "tenant_id", "student_id", "guardian_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_students_tenant_id",
                table: "students",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_tenant_id_admission_number",
                table: "students",
                columns: new[] { "tenant_id", "admission_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_students_tenant_id_status",
                table: "students",
                columns: new[] { "tenant_id", "status" });

            // Second tenant-isolation layer for every SIS table.
            migrationBuilder.EnableTenantRls("school_classes");
            migrationBuilder.EnableTenantRls("sections");
            migrationBuilder.EnableTenantRls("students");
            migrationBuilder.EnableTenantRls("guardians");
            migrationBuilder.EnableTenantRls("student_guardians");
            migrationBuilder.EnableTenantRls("enrollments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("enrollments");
            migrationBuilder.DisableTenantRls("student_guardians");
            migrationBuilder.DisableTenantRls("guardians");
            migrationBuilder.DisableTenantRls("students");
            migrationBuilder.DisableTenantRls("sections");
            migrationBuilder.DisableTenantRls("school_classes");

            migrationBuilder.DropTable(
                name: "enrollments");

            migrationBuilder.DropTable(
                name: "student_guardians");

            migrationBuilder.DropTable(
                name: "sections");

            migrationBuilder.DropTable(
                name: "guardians");

            migrationBuilder.DropTable(
                name: "students");

            migrationBuilder.DropTable(
                name: "school_classes");
        }
    }
}
