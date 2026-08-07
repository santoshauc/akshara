using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Outbox;
using SchoolErp.Domain.Students;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for attendance records.</summary>
public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("attendance_records");

        builder.Property(a => a.Remarks).HasMaxLength(256);

        // One record per placement per day.
        builder.HasIndex(a => new { a.TenantId, a.EnrollmentId, a.Date }).IsUnique();
        // Marking grid: whole section for a date.
        builder.HasIndex(a => new { a.TenantId, a.SectionId, a.Date });
        // Parent calendar: one student across a month.
        builder.HasIndex(a => new { a.TenantId, a.StudentId, a.Date });

        builder.HasOne<Enrollment>().WithMany()
            .HasForeignKey(a => a.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for the transactional outbox (no RLS — see entity docs).</summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.Property(m => m.Type).HasMaxLength(32).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(1024);

        // Dispatcher scan: unprocessed, oldest first.
        builder.HasIndex(m => m.CreatedAt).HasFilter("processed_at IS NULL");
    }
}
