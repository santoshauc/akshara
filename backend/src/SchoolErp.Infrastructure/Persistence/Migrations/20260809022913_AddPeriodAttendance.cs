using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_attendance_records_tenant_id_enrollment_id_date",
                table: "attendance_records");

            migrationBuilder.AddColumn<int>(
                name: "period",
                table: "attendance_records",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_daily_unique",
                table: "attendance_records",
                columns: new[] { "tenant_id", "enrollment_id", "date" },
                unique: true,
                filter: "period IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_period_unique",
                table: "attendance_records",
                columns: new[] { "tenant_id", "enrollment_id", "date", "period" },
                unique: true,
                filter: "period IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_attendance_records_daily_unique",
                table: "attendance_records");

            migrationBuilder.DropIndex(
                name: "ix_attendance_records_period_unique",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "period",
                table: "attendance_records");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_tenant_id_enrollment_id_date",
                table: "attendance_records",
                columns: new[] { "tenant_id", "enrollment_id", "date" },
                unique: true);
        }
    }
}
