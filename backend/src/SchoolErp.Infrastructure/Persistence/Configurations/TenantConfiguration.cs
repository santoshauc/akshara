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
        builder.HasMany(t => t.Affiliations)
            .WithOne()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Resolution identifiers must be unique across the platform.
        builder.HasIndex(t => t.Code).IsUnique();
        builder.HasIndex(t => t.Subdomain).IsUnique();
        builder.HasIndex(t => t.CustomDomain).IsUnique();

        builder.Ignore(t => t.IsActive);
    }
}

/// <summary>
/// Mapping for a school's board affiliations. No RLS: this is catalog data
/// hanging off <c>tenants</c>, which has none either — a documented
/// platform-scoped exception. Callers scope by TenantId explicitly.
/// </summary>
public sealed class TenantAffiliationConfiguration : IEntityTypeConfiguration<TenantAffiliation>
{
    public void Configure(EntityTypeBuilder<TenantAffiliation> builder)
    {
        builder.ToTable("tenant_affiliations");
        builder.Property(a => a.Board).HasMaxLength(64).IsRequired();
        builder.Property(a => a.AffiliationNumber).HasMaxLength(64);

        // A school is affiliated to a given board once.
        builder.HasIndex(a => new { a.TenantId, a.Board }).IsUnique();
    }
}
