using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Billing;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for platform invoices (no RLS — platform-scoped, see entity docs).</summary>
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.Property(i => i.InvoiceNumber).HasMaxLength(32).IsRequired();
        builder.Property(i => i.TotalAmount).HasPrecision(12, 2);
        builder.Property(i => i.Notes).HasMaxLength(1000);

        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.Status });

        builder.HasMany(i => i.Lines)
            .WithOne()
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Mapping for invoice lines.</summary>
public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("invoice_lines");
        builder.Property(l => l.Description).HasMaxLength(300).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(10, 2);
        builder.Property(l => l.UnitAmount).HasPrecision(12, 2);
        builder.Property(l => l.Amount).HasPrecision(12, 2);
    }
}
