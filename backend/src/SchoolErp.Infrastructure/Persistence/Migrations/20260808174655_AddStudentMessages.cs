using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sent_by_staff = table.Column<bool>(type: "boolean", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    body = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    read_by_parent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    read_by_staff_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_student_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_messages_tenant_id",
                table: "student_messages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_messages_tenant_id_student_id_created_at",
                table: "student_messages",
                columns: new[] { "tenant_id", "student_id", "created_at" });

            migrationBuilder.EnableTenantRls("student_messages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("student_messages");
            migrationBuilder.DropTable(
                name: "student_messages");
        }
    }
}
