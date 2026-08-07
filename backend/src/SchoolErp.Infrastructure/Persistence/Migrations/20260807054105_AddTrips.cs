using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    inspection_ok = table.Column<bool>(type: "boolean", nullable: false),
                    inspection_notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("pk_trips", x => x.id);
                    table.ForeignKey(
                        name: "fk_trips_transport_routes_route_id",
                        column: x => x.route_id,
                        principalTable: "transport_routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trip_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_trip_locations", x => x.id);
                    table.ForeignKey(
                        name: "fk_trip_locations_trips_trip_id",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trip_student_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<int>(type: "integer", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    remarks = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("pk_trip_student_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_trip_student_events_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trip_student_events_trips_trip_id",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trip_locations_tenant_id",
                table: "trip_locations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_trip_locations_tenant_id_trip_id_recorded_at",
                table: "trip_locations",
                columns: new[] { "tenant_id", "trip_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_trip_locations_trip_id",
                table: "trip_locations",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "ix_trip_student_events_student_id",
                table: "trip_student_events",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_trip_student_events_tenant_id",
                table: "trip_student_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_trip_student_events_tenant_id_trip_id_student_id_event_type",
                table: "trip_student_events",
                columns: new[] { "tenant_id", "trip_id", "student_id", "event_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trip_student_events_trip_id",
                table: "trip_student_events",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "ix_trips_route_id",
                table: "trips",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "ix_trips_tenant_id",
                table: "trips",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_trips_tenant_id_route_id_status",
                table: "trips",
                columns: new[] { "tenant_id", "route_id", "status" });

            migrationBuilder.EnableTenantRls("trips");
            migrationBuilder.EnableTenantRls("trip_locations");
            migrationBuilder.EnableTenantRls("trip_student_events");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("trip_student_events");
            migrationBuilder.DisableTenantRls("trip_locations");
            migrationBuilder.DisableTenantRls("trips");

            migrationBuilder.DropTable(
                name: "trip_locations");

            migrationBuilder.DropTable(
                name: "trip_student_events");

            migrationBuilder.DropTable(
                name: "trips");
        }
    }
}
