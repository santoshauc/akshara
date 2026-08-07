using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.Transport;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for vehicles.</summary>
public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");
        builder.Property(v => v.RegistrationNumber).HasMaxLength(16).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(64);
        builder.HasIndex(v => new { v.TenantId, v.RegistrationNumber }).IsUnique();
    }
}

/// <summary>Mapping for routes.</summary>
public sealed class TransportRouteConfiguration : IEntityTypeConfiguration<TransportRoute>
{
    public void Configure(EntityTypeBuilder<TransportRoute> builder)
    {
        builder.ToTable("transport_routes");
        builder.Property(r => r.Name).HasMaxLength(64).IsRequired();
        builder.Property(r => r.DriverName).HasMaxLength(128);
        builder.Property(r => r.DriverPhone).HasMaxLength(20);
        builder.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();

        builder.HasOne(r => r.Vehicle).WithMany()
            .HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Stops).WithOne()
            .HasForeignKey(s => s.RouteId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for stops.</summary>
public sealed class RouteStopConfiguration : IEntityTypeConfiguration<RouteStop>
{
    public void Configure(EntityTypeBuilder<RouteStop> builder)
    {
        builder.ToTable("route_stops");
        builder.Property(s => s.Name).HasMaxLength(128).IsRequired();
        builder.Property(s => s.Latitude).HasPrecision(9, 6);
        builder.Property(s => s.Longitude).HasPrecision(9, 6);
        builder.HasIndex(s => new { s.TenantId, s.RouteId, s.SortOrder }).IsUnique();
    }
}

/// <summary>Mapping for student allocations.</summary>
public sealed class StudentTransportAssignmentConfiguration
    : IEntityTypeConfiguration<StudentTransportAssignment>
{
    public void Configure(EntityTypeBuilder<StudentTransportAssignment> builder)
    {
        builder.ToTable("student_transport_assignments");

        // One allocation per student.
        builder.HasIndex(a => new { a.TenantId, a.StudentId }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.RouteId });

        builder.HasOne<Student>().WithMany()
            .HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Route).WithMany()
            .HasForeignKey(a => a.RouteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Stop).WithMany()
            .HasForeignKey(a => a.StopId).OnDelete(DeleteBehavior.Restrict);
    }
}
