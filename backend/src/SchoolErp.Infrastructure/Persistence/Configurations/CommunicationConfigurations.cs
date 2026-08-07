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
