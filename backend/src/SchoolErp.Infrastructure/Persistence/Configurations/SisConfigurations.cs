using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Students;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for classes and sections.</summary>
public sealed class SchoolClassConfiguration : IEntityTypeConfiguration<SchoolClass>
{
    public void Configure(EntityTypeBuilder<SchoolClass> builder)
    {
        builder.ToTable("school_classes");
        builder.Property(c => c.Name).HasMaxLength(64).IsRequired();
        builder.HasIndex(c => new { c.TenantId, c.Name }).IsUnique();

        builder.HasMany(c => c.Sections)
            .WithOne()
            .HasForeignKey(s => s.SchoolClassId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for sections.</summary>
public sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("sections");
        builder.Property(s => s.Name).HasMaxLength(16).IsRequired();
        builder.HasIndex(s => new { s.TenantId, s.SchoolClassId, s.Name }).IsUnique();
    }
}

/// <summary>Mapping for students.</summary>
public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students");

        builder.Property(s => s.AdmissionNumber).HasMaxLength(32).IsRequired();
        builder.Property(s => s.FirstName).HasMaxLength(64).IsRequired();
        builder.Property(s => s.LastName).HasMaxLength(64).IsRequired();
        builder.Property(s => s.BloodGroup).HasMaxLength(8);
        builder.Property(s => s.Email).HasMaxLength(320);
        builder.Property(s => s.Phone).HasMaxLength(20);
        builder.Property(s => s.AddressLine1).HasMaxLength(256);
        builder.Property(s => s.City).HasMaxLength(64);
        builder.Property(s => s.State).HasMaxLength(64);
        builder.Property(s => s.PostalCode).HasMaxLength(16);
        builder.Property(s => s.PhotoUrl).HasMaxLength(1024);
        builder.Property(s => s.MedicalNotes).HasMaxLength(2048);

        builder.HasIndex(s => new { s.TenantId, s.AdmissionNumber }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.Status });

        builder.Ignore(s => s.FullName);

        builder.HasMany(s => s.Guardians)
            .WithOne()
            .HasForeignKey(g => g.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Enrollments)
            .WithOne()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for guardians.</summary>
public sealed class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> builder)
    {
        builder.ToTable("guardians");

        builder.Property(g => g.FirstName).HasMaxLength(64).IsRequired();
        builder.Property(g => g.LastName).HasMaxLength(64).IsRequired();
        builder.Property(g => g.Phone).HasMaxLength(20).IsRequired();
        builder.Property(g => g.Email).HasMaxLength(320);
        builder.Property(g => g.Occupation).HasMaxLength(128);
        builder.Property(g => g.PreferredLanguage).HasMaxLength(8).IsRequired().HasDefaultValue("en");

        // Sibling admissions reuse the guardian found by phone.
        builder.HasIndex(g => new { g.TenantId, g.Phone });

        builder.Ignore(g => g.FullName);
    }
}

/// <summary>Mapping for the student↔guardian link.</summary>
public sealed class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> builder)
    {
        builder.ToTable("student_guardians");

        builder.HasIndex(sg => new { sg.TenantId, sg.StudentId, sg.GuardianId }).IsUnique();

        builder.HasOne(sg => sg.Guardian)
            .WithMany()
            .HasForeignKey(sg => sg.GuardianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for enrollments.</summary>
public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments");

        // One placement per student per academic year.
        builder.HasIndex(e => new { e.TenantId, e.StudentId, e.AcademicYearId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.AcademicYearId, e.SchoolClassId, e.SectionId });

        builder.HasOne(e => e.AcademicYear).WithMany()
            .HasForeignKey(e => e.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SchoolClass).WithMany()
            .HasForeignKey(e => e.SchoolClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Section).WithMany()
            .HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
