using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryAndHostel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "books",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    author = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    isbn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    copies_total = table.Column<int>(type: "integer", nullable: false),
                    copies_available = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_books", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hostels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    warden_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    warden_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("pk_hostels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "book_loans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    returned_on = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_book_loans", x => x.id);
                    table.ForeignKey(
                        name: "fk_book_loans_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_book_loans_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hostel_rooms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hostel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_hostel_rooms", x => x.id);
                    table.ForeignKey(
                        name: "fk_hostel_rooms_hostels_hostel_id",
                        column: x => x.hostel_id,
                        principalTable: "hostels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hostel_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    vacated_on = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_hostel_allocations", x => x.id);
                    table.ForeignKey(
                        name: "fk_hostel_allocations_hostel_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "hostel_rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_hostel_allocations_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_book_loans_book_id",
                table: "book_loans",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_loans_student_id",
                table: "book_loans",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_loans_tenant_id",
                table: "book_loans",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_loans_tenant_id_book_id_returned_on",
                table: "book_loans",
                columns: new[] { "tenant_id", "book_id", "returned_on" });

            migrationBuilder.CreateIndex(
                name: "ix_book_loans_tenant_id_student_id_returned_on",
                table: "book_loans",
                columns: new[] { "tenant_id", "student_id", "returned_on" });

            migrationBuilder.CreateIndex(
                name: "ix_books_tenant_id",
                table: "books",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_books_tenant_id_title",
                table: "books",
                columns: new[] { "tenant_id", "title" });

            migrationBuilder.CreateIndex(
                name: "ix_hostel_allocations_room_id",
                table: "hostel_allocations",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_hostel_allocations_student_id",
                table: "hostel_allocations",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_hostel_allocations_tenant_id",
                table: "hostel_allocations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_hostel_allocations_tenant_id_room_id_vacated_on",
                table: "hostel_allocations",
                columns: new[] { "tenant_id", "room_id", "vacated_on" });

            migrationBuilder.CreateIndex(
                name: "ix_hostel_allocations_tenant_id_student_id_vacated_on",
                table: "hostel_allocations",
                columns: new[] { "tenant_id", "student_id", "vacated_on" });

            migrationBuilder.CreateIndex(
                name: "ix_hostel_rooms_hostel_id",
                table: "hostel_rooms",
                column: "hostel_id");

            migrationBuilder.CreateIndex(
                name: "ix_hostel_rooms_tenant_id",
                table: "hostel_rooms",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_hostel_rooms_tenant_id_hostel_id_room_number",
                table: "hostel_rooms",
                columns: new[] { "tenant_id", "hostel_id", "room_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hostels_tenant_id",
                table: "hostels",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_hostels_tenant_id_name",
                table: "hostels",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.EnableTenantRls("books");
            migrationBuilder.EnableTenantRls("book_loans");
            migrationBuilder.EnableTenantRls("hostels");
            migrationBuilder.EnableTenantRls("hostel_rooms");
            migrationBuilder.EnableTenantRls("hostel_allocations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("hostel_allocations");
            migrationBuilder.DisableTenantRls("hostel_rooms");
            migrationBuilder.DisableTenantRls("hostels");
            migrationBuilder.DisableTenantRls("book_loans");
            migrationBuilder.DisableTenantRls("books");

            migrationBuilder.DropTable(
                name: "book_loans");

            migrationBuilder.DropTable(
                name: "hostel_allocations");

            migrationBuilder.DropTable(
                name: "books");

            migrationBuilder.DropTable(
                name: "hostel_rooms");

            migrationBuilder.DropTable(
                name: "hostels");
        }
    }
}
