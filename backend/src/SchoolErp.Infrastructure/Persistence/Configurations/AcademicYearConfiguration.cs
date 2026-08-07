using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for academic years (tenant-scoped).</summary>
public sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("academic_years");

        builder.Property(y => y.Name).HasMaxLength(32).IsRequired();

        // Year names repeat across schools but must be unique within one.
        builder.HasIndex(y => new { y.TenantId, y.Name }).IsUnique();

        // Referential integrity back to the catalog; no navigation is exposed
        // because business code must never traverse into other tenants' data.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(y => y.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
