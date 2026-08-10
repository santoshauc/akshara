using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentProgramme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "programme_id",
                table: "enrollments",
                type: "uuid",
                nullable: true);

            // Existing college enrollments already have a programme — it is
            // just implied by their cohort. Copy it across, or every student
            // admitted before this column would count as belonging to no
            // programme at all.
            //
            // FORCE has to come off for the duration. app_current_tenant_id()
            // returns NULL when app.tenant_id is unset, as it is during a
            // migration, so `tenant_id = NULL` is NULL and the policy's USING
            // clause hides EVERY row from the owner. The UPDATE would not
            // fail — it would report success having changed nothing, which is
            // the worst possible outcome for a backfill.
            migrationBuilder.Sql("""
                ALTER TABLE enrollments NO FORCE ROW LEVEL SECURITY;

                UPDATE enrollments e
                SET programme_id = c.programme_id
                FROM school_classes c
                WHERE c.id = e.school_class_id
                  AND c.programme_id IS NOT NULL;

                ALTER TABLE enrollments FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "programme_id",
                table: "enrollments");
        }
    }
}
