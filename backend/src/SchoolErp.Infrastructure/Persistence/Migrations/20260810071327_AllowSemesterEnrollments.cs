using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowSemesterEnrollments : Migration
    {
        /// <summary>
        /// The old index said one enrollment per student per academic year.
        /// True of a school; false of a college, where an odd and an even
        /// semester both sit inside one academic year. Narrowed to one ACTIVE
        /// enrollment, which is the invariant that actually matters — a
        /// student is never in two places at once — while the closed rows
        /// stand as history.
        ///
        /// Down is only reversible while no tenant has run a semester cycle:
        /// recreating the strict index fails once one has, which is the
        /// correct outcome rather than silently discarding placements.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_enrollments_tenant_id_student_id_academic_year_id",
                table: "enrollments");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_tenant_id_student_id_academic_year_id",
                table: "enrollments",
                columns: new[] { "tenant_id", "student_id", "academic_year_id" },
                unique: true,
                filter: "status = 1 AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_enrollments_tenant_id_student_id_academic_year_id",
                table: "enrollments");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_tenant_id_student_id_academic_year_id",
                table: "enrollments",
                columns: new[] { "tenant_id", "student_id", "academic_year_id" },
                unique: true);
        }
    }
}
