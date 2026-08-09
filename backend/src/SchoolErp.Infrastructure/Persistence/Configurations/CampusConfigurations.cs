using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Campuses;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for an institution's campuses.</summary>
public sealed class CampusConfiguration : IEntityTypeConfiguration<Campus>
{
    public void Configure(EntityTypeBuilder<Campus> builder)
    {
        builder.ToTable("campuses");

        builder.Property(c => c.Name).HasMaxLength(128).IsRequired();
        builder.Property(c => c.Code).HasMaxLength(16).IsRequired();
        builder.Property(c => c.AddressLine1).HasMaxLength(256);
        builder.Property(c => c.City).HasMaxLength(128);
        builder.Property(c => c.State).HasMaxLength(128);
        builder.Property(c => c.PostalCode).HasMaxLength(16);
        builder.Property(c => c.ContactPhone).HasMaxLength(20);

        // A campus code is how staff refer to it, so it must be unambiguous
        // within the institution.
        builder.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.IsActive });
    }
}
