using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Notifications;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for device push tokens.</summary>
public sealed class PushTokenConfiguration : IEntityTypeConfiguration<PushToken>
{
    public void Configure(EntityTypeBuilder<PushToken> builder)
    {
        builder.ToTable("push_tokens");
        builder.Property(t => t.Phone).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Token).HasMaxLength(256).IsRequired();
        builder.Property(t => t.Platform).HasMaxLength(16).IsRequired();

        // One row per physical device; re-registration upserts.
        builder.HasIndex(t => new { t.TenantId, t.Token }).IsUnique();
        builder.HasIndex(t => new { t.TenantId, t.Phone });
    }
}
