using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    registration_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    insurance_expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    fitness_expiry = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_vehicles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transport_routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    driver_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    driver_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    driver_user_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_transport_routes", x => x.id);
                    table.ForeignKey(
                        name: "fk_transport_routes_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "route_stops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    pickup_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
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
                    table.PrimaryKey("pk_route_stops", x => x.id);
                    table.ForeignKey(
                        name: "fk_route_stops_transport_routes_route_id",
                        column: x => x.route_id,
                        principalTable: "transport_routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_transport_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stop_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_student_transport_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_transport_assignments_route_stops_stop_id",
                        column: x => x.stop_id,
                        principalTable: "route_stops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_transport_assignments_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_transport_assignments_transport_routes_route_id",
                        column: x => x.route_id,
                        principalTable: "transport_routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_route_stops_route_id",
                table: "route_stops",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_stops_tenant_id",
                table: "route_stops",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_stops_tenant_id_route_id_sort_order",
                table: "route_stops",
                columns: new[] { "tenant_id", "route_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_transport_assignments_route_id",
                table: "student_transport_assignments",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_transport_assignments_stop_id",
                table: "student_transport_assignments",
                column: "stop_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_transport_assignments_student_id",
                table: "student_transport_assignments",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_transport_assignments_tenant_id",
                table: "student_transport_assignments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_transport_assignments_tenant_id_route_id",
                table: "student_transport_assignments",
                columns: new[] { "tenant_id", "route_id" });

            migrationBuilder.CreateIndex(
                name: "ix_student_transport_assignments_tenant_id_student_id",
                table: "student_transport_assignments",
                columns: new[] { "tenant_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transport_routes_tenant_id",
                table: "transport_routes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_transport_routes_tenant_id_name",
                table: "transport_routes",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transport_routes_vehicle_id",
                table: "transport_routes",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_tenant_id",
                table: "vehicles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_tenant_id_registration_number",
                table: "vehicles",
                columns: new[] { "tenant_id", "registration_number" },
                unique: true);

            migrationBuilder.EnableTenantRls("vehicles");
            migrationBuilder.EnableTenantRls("transport_routes");
            migrationBuilder.EnableTenantRls("route_stops");
            migrationBuilder.EnableTenantRls("student_transport_assignments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("student_transport_assignments");
            migrationBuilder.DisableTenantRls("route_stops");
            migrationBuilder.DisableTenantRls("transport_routes");
            migrationBuilder.DisableTenantRls("vehicles");

            migrationBuilder.DropTable(
                name: "student_transport_assignments");

            migrationBuilder.DropTable(
                name: "route_stops");

            migrationBuilder.DropTable(
                name: "transport_routes");

            migrationBuilder.DropTable(
                name: "vehicles");
        }
    }
}
