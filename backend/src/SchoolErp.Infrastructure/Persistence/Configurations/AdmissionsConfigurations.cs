using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Admissions;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for admission enquiries.</summary>
public sealed class AdmissionEnquiryConfiguration : IEntityTypeConfiguration<AdmissionEnquiry>
{
    public void Configure(EntityTypeBuilder<AdmissionEnquiry> builder)
    {
        builder.ToTable("admission_enquiries");
        builder.Property(e => e.ChildName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.AppliedClass).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ParentName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(320);
        builder.Property(e => e.Notes).HasMaxLength(2000);

        // The hot queries: pipeline board by status, and today's follow-up list.
        builder.HasIndex(e => new { e.TenantId, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.FollowUpOn });
    }
}
