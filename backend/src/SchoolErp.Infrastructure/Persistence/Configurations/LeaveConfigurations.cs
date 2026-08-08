using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Leave;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for leave requests.</summary>
public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("leave_requests");
        builder.Property(l => l.Reason).HasMaxLength(512).IsRequired();
        builder.Property(l => l.DecisionNote).HasMaxLength(512);

        // The two hot queries: staff inbox by status, and one applicant's history.
        builder.HasIndex(l => new { l.TenantId, l.Status });
        builder.HasIndex(l => new { l.TenantId, l.StudentId });
        builder.HasIndex(l => new { l.TenantId, l.RequestedByUserId });
    }
}
