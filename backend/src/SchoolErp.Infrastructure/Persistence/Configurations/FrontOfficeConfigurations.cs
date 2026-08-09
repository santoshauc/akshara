using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.FrontOffice;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for the gate visitor register.</summary>
public sealed class VisitorEntryConfiguration : IEntityTypeConfiguration<VisitorEntry>
{
    public void Configure(EntityTypeBuilder<VisitorEntry> builder)
    {
        builder.ToTable("visitor_entries");
        builder.Property(v => v.VisitorName).HasMaxLength(128).IsRequired();
        builder.Property(v => v.Phone).HasMaxLength(20);
        builder.Property(v => v.WhomToMeet).HasMaxLength(128);
        builder.Property(v => v.PassNumber).HasMaxLength(24).IsRequired();
        builder.Property(v => v.Remarks).HasMaxLength(512);
        // The desk's two hot queries: who is inside now, and today's register.
        builder.HasIndex(v => new { v.TenantId, v.CheckedOutAt });
        builder.HasIndex(v => new { v.TenantId, v.CheckedInAt });
        builder.HasOne(v => v.Student).WithMany().HasForeignKey(v => v.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for student gate passes.</summary>
public sealed class GatePassConfiguration : IEntityTypeConfiguration<GatePass>
{
    public void Configure(EntityTypeBuilder<GatePass> builder)
    {
        builder.ToTable("gate_passes");
        builder.Property(g => g.PassNumber).HasMaxLength(24).IsRequired();
        builder.Property(g => g.Reason).HasMaxLength(256).IsRequired();
        builder.Property(g => g.ReleasedTo).HasMaxLength(128).IsRequired();
        builder.Property(g => g.ReleasedToPhone).HasMaxLength(20);
        builder.HasIndex(g => new { g.TenantId, g.PassNumber }).IsUnique();
        builder.HasIndex(g => new { g.TenantId, g.IssuedAt });
        builder.HasOne(g => g.Student).WithMany().HasForeignKey(g => g.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
