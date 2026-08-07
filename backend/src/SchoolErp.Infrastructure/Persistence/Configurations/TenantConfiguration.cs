using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for the platform tenant catalog.</summary>
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.Property(t => t.Code).HasMaxLength(16).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(256).IsRequired();
        builder.Property(t => t.Subdomain).HasMaxLength(63).IsRequired();
        builder.Property(t => t.CustomDomain).HasMaxLength(253);
        builder.Property(t => t.TimeZoneId).HasMaxLength(64);
        builder.Property(t => t.DefaultLanguage).HasMaxLength(8);
        builder.Property(t => t.ThemePrimaryColor).HasMaxLength(16);
        builder.Property(t => t.ThemeSecondaryColor).HasMaxLength(16);
        builder.Property(t => t.LogoUrl).HasMaxLength(1024);
        builder.Property(t => t.ContactEmail).HasMaxLength(320);
        builder.Property(t => t.ContactPhone).HasMaxLength(20);
        builder.Property(t => t.AffiliationBoard).HasMaxLength(64);
        builder.Property(t => t.AffiliationNumber).HasMaxLength(64);

        // Resolution identifiers must be unique across the platform.
        builder.HasIndex(t => t.Code).IsUnique();
        builder.HasIndex(t => t.Subdomain).IsUnique();
        builder.HasIndex(t => t.CustomDomain).IsUnique();

        builder.Ignore(t => t.IsActive);
    }
}
