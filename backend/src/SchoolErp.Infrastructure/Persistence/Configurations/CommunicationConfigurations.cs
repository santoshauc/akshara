using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Communication;
using SchoolErp.Domain.Homework;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for notices.</summary>
public sealed class NoticeConfiguration : IEntityTypeConfiguration<Notice>
{
    public void Configure(EntityTypeBuilder<Notice> builder)
    {
        builder.ToTable("notices");
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(4000).IsRequired();
        builder.HasIndex(n => new { n.TenantId, n.CreatedAt });
    }
}

/// <summary>Mapping for homework assignments.</summary>
public sealed class HomeworkAssignmentConfiguration : IEntityTypeConfiguration<HomeworkAssignment>
{
    public void Configure(EntityTypeBuilder<HomeworkAssignment> builder)
    {
        builder.ToTable("homework_assignments");
        builder.Property(h => h.Title).HasMaxLength(200).IsRequired();
        builder.Property(h => h.Instructions).HasMaxLength(4000).IsRequired();

        builder.HasIndex(h => new { h.TenantId, h.SchoolClassId, h.DueDate });

        builder.HasOne(h => h.SchoolClass).WithMany()
            .HasForeignKey(h => h.SchoolClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.Subject).WithMany()
            .HasForeignKey(h => h.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for parent↔school messages.</summary>
public sealed class StudentMessageConfiguration
    : IEntityTypeConfiguration<Domain.Communication.StudentMessage>
{
    public void Configure(EntityTypeBuilder<Domain.Communication.StudentMessage> builder)
    {
        builder.ToTable("student_messages");
        builder.Property(m => m.SenderName).HasMaxLength(128).IsRequired();
        builder.Property(m => m.Body).HasMaxLength(2048).IsRequired();
        builder.HasIndex(m => new { m.TenantId, m.StudentId, m.CreatedAt });
    }
}
