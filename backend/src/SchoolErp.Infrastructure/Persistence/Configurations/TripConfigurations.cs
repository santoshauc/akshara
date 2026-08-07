using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.Transport;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for trips.</summary>
public sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("trips");
        builder.Property(t => t.InspectionNotes).HasMaxLength(512);
        builder.HasIndex(t => new { t.TenantId, t.RouteId, t.Status });

        builder.HasOne(t => t.Route).WithMany()
            .HasForeignKey(t => t.RouteId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for GPS pings.</summary>
public sealed class TripLocationConfiguration : IEntityTypeConfiguration<TripLocation>
{
    public void Configure(EntityTypeBuilder<TripLocation> builder)
    {
        builder.ToTable("trip_locations");
        builder.Property(l => l.Latitude).HasPrecision(9, 6);
        builder.Property(l => l.Longitude).HasPrecision(9, 6);
        // Latest-ping lookup for live tracking.
        builder.HasIndex(l => new { l.TenantId, l.TripId, l.RecordedAt });

        builder.HasOne<Trip>().WithMany()
            .HasForeignKey(l => l.TripId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for per-student trip events.</summary>
public sealed class TripStudentEventConfiguration : IEntityTypeConfiguration<TripStudentEvent>
{
    public void Configure(EntityTypeBuilder<TripStudentEvent> builder)
    {
        builder.ToTable("trip_student_events");
        builder.Property(e => e.Remarks).HasMaxLength(256);
        // One event of a kind per student per trip (no duplicate boarded-SMS).
        builder.HasIndex(e => new { e.TenantId, e.TripId, e.StudentId, e.EventType }).IsUnique();

        builder.HasOne<Trip>().WithMany()
            .HasForeignKey(e => e.TripId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Student>().WithMany()
            .HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
