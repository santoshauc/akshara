using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Timetable;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for timetable entries.</summary>
public sealed class TimetableEntryConfiguration : IEntityTypeConfiguration<TimetableEntry>
{
    public void Configure(EntityTypeBuilder<TimetableEntry> builder)
    {
        builder.ToTable("timetable_entries");
        builder.Property(t => t.TeacherName).HasMaxLength(128);

        // Weekly grid lookups per class scope. Uniqueness of (day, period)
        // within a scope is guaranteed by the full-replace define command.
        builder.HasIndex(t => new { t.TenantId, t.SchoolClassId, t.DayOfWeek });

        builder.HasOne(t => t.Subject).WithMany()
            .HasForeignKey(t => t.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
