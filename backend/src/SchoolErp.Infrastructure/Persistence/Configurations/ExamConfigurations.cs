using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Students;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for subjects.</summary>
public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("subjects");
        builder.Property(s => s.Name).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Code).HasMaxLength(16).IsRequired();
        builder.HasIndex(s => new { s.TenantId, s.Name }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.Code }).IsUnique();
    }
}

/// <summary>Mapping for exams.</summary>
public sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("exams");
        builder.Property(e => e.Name).HasMaxLength(128).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.AcademicYearId, e.Name }).IsUnique();

        builder.HasOne(e => e.AcademicYear).WithMany()
            .HasForeignKey(e => e.AcademicYearId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Subjects).WithOne()
            .HasForeignKey(s => s.ExamId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for scheduled papers.</summary>
public sealed class ExamSubjectConfiguration : IEntityTypeConfiguration<ExamSubject>
{
    public void Configure(EntityTypeBuilder<ExamSubject> builder)
    {
        builder.ToTable("exam_subjects");
        builder.Property(s => s.MaxMarks).HasPrecision(6, 2);
        builder.Property(s => s.PassMarks).HasPrecision(6, 2);

        // One paper per subject per class within an exam.
        builder.HasIndex(s => new { s.TenantId, s.ExamId, s.SchoolClassId, s.SubjectId }).IsUnique();

        builder.HasOne(s => s.SchoolClass).WithMany()
            .HasForeignKey(s => s.SchoolClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Subject).WithMany()
            .HasForeignKey(s => s.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for mark entries.</summary>
public sealed class MarkEntryConfiguration : IEntityTypeConfiguration<MarkEntry>
{
    public void Configure(EntityTypeBuilder<MarkEntry> builder)
    {
        builder.ToTable("mark_entries");
        builder.Property(m => m.MarksObtained).HasPrecision(6, 2);

        // One mark row per student per paper.
        builder.HasIndex(m => new { m.TenantId, m.ExamSubjectId, m.EnrollmentId }).IsUnique();
        // Student-centric result composition.
        builder.HasIndex(m => new { m.TenantId, m.StudentId });

        builder.HasOne(m => m.ExamSubject).WithMany()
            .HasForeignKey(m => m.ExamSubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Enrollment>().WithMany()
            .HasForeignKey(m => m.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
