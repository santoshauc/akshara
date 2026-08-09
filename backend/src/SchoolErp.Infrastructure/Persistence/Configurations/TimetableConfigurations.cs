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

        builder.HasOne(t => t.Teacher).WithMany()
            .HasForeignKey(t => t.TeacherId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for day-specific substitutions.</summary>
public sealed class TimetableSubstitutionConfiguration
    : IEntityTypeConfiguration<Domain.Timetable.TimetableSubstitution>
{
    public void Configure(EntityTypeBuilder<Domain.Timetable.TimetableSubstitution> builder)
    {
        builder.ToTable("timetable_substitutions");
        builder.HasIndex(s => new { s.TenantId, s.Date, s.TimetableEntryId }).IsUnique();
        builder.HasOne(s => s.TimetableEntry).WithMany()
            .HasForeignKey(s => s.TimetableEntryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.SubstituteTeacher).WithMany()
            .HasForeignKey(s => s.SubstituteTeacherId).OnDelete(DeleteBehavior.Restrict);
    }
}
