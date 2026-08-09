using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Fees;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for fee heads.</summary>
public sealed class FeeHeadConfiguration : IEntityTypeConfiguration<FeeHead>
{
    public void Configure(EntityTypeBuilder<FeeHead> builder)
    {
        builder.ToTable("fee_heads");
        builder.Property(h => h.Name).HasMaxLength(64).IsRequired();
        builder.HasIndex(h => new { h.TenantId, h.Name }).IsUnique();
    }
}

/// <summary>Mapping for per-student concessions.</summary>
public sealed class FeeConcessionConfiguration : IEntityTypeConfiguration<FeeConcession>
{
    public void Configure(EntityTypeBuilder<FeeConcession> builder)
    {
        builder.ToTable("fee_concessions");
        builder.Property(c => c.Reason).HasMaxLength(256).IsRequired();
        builder.HasIndex(c => new { c.TenantId, c.StudentId, c.AcademicYearId });
        builder.HasOne(c => c.FeeHead).WithMany()
            .HasForeignKey(c => c.FeeHeadId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for fee structure items.</summary>
public sealed class FeeStructureItemConfiguration : IEntityTypeConfiguration<FeeStructureItem>
{
    public void Configure(EntityTypeBuilder<FeeStructureItem> builder)
    {
        builder.ToTable("fee_structure_items");
        builder.Property(i => i.Amount).HasPrecision(10, 2);
        builder.Property(i => i.Label).HasMaxLength(50);

        // One head can appear multiple times (installments) but not twice on the same due date.
        builder.HasIndex(i => new { i.TenantId, i.AcademicYearId, i.SchoolClassId, i.FeeHeadId, i.DueDate })
            .IsUnique();

        builder.HasOne(i => i.FeeHead).WithMany()
            .HasForeignKey(i => i.FeeHeadId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for fee payments.</summary>
public sealed class FeePaymentConfiguration : IEntityTypeConfiguration<FeePayment>
{
    public void Configure(EntityTypeBuilder<FeePayment> builder)
    {
        builder.ToTable("fee_payments");
        builder.Property(p => p.Amount).HasPrecision(10, 2);
        builder.Property(p => p.ReceiptNumber).HasMaxLength(32).IsRequired();
        builder.Property(p => p.Reference).HasMaxLength(128);
        builder.Property(p => p.Remarks).HasMaxLength(256);

        builder.HasIndex(p => new { p.TenantId, p.ReceiptNumber }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.StudentId, p.AcademicYearId });
    }
}

/// <summary>Mapping for payment orders (no RLS — webhook-scoped, see entity docs).</summary>
public sealed class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(EntityTypeBuilder<PaymentOrder> builder)
    {
        builder.ToTable("payment_orders");
        builder.Property(o => o.Amount).HasPrecision(10, 2);
        builder.Property(o => o.GatewayOrderId).HasMaxLength(64).IsRequired();
        builder.Property(o => o.GatewayPaymentId).HasMaxLength(64);

        builder.HasIndex(o => o.GatewayOrderId).IsUnique();
    }
}
