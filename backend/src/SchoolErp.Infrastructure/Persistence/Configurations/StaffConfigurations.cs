using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Staff;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for teaching staff.</summary>
public sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("teachers");
        builder.Property(t => t.EmployeeCode).HasMaxLength(32).IsRequired();
        builder.Property(t => t.FullName).HasMaxLength(128).IsRequired();
        builder.Property(t => t.Phone).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Email).HasMaxLength(256);
        builder.Property(t => t.Qualification).HasMaxLength(256);
        builder.Property(t => t.Specialization).HasMaxLength(256);

        builder.HasIndex(t => new { t.TenantId, t.EmployeeCode }).IsUnique();
        builder.HasIndex(t => new { t.TenantId, t.Phone }).IsUnique();
    }
}
