using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Audit;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for audit events. No RLS: TenantId is nullable (platform actions)
/// and the query handler filters by tenant explicitly.
/// </summary>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.Property(a => a.UserId).HasMaxLength(64);
        builder.Property(a => a.UserName).HasMaxLength(128);
        builder.Property(a => a.Action).HasMaxLength(128).IsRequired();
        builder.Property(a => a.Detail).HasMaxLength(512);
        builder.Property(a => a.IpAddress).HasMaxLength(45);

        // The trail is always read newest-first within a school.
        builder.HasIndex(a => new { a.TenantId, a.OccurredAt }).IsDescending(false, true);
    }
}
