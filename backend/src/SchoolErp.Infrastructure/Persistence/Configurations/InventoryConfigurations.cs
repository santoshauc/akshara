using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Inventory;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for the store catalogue.</summary>
public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("inventory_items");
        builder.Property(i => i.Name).HasMaxLength(128).IsRequired();
        builder.Property(i => i.Category).HasMaxLength(64);
        builder.Property(i => i.Unit).HasMaxLength(16).IsRequired();
        builder.Property(i => i.UnitCost).HasPrecision(12, 2);
        // Names are the store's natural key — duplicates make the register lie.
        builder.HasIndex(i => new { i.TenantId, i.Name }).IsUnique();
    }
}

/// <summary>Mapping for the append-only stock register.</summary>
public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");
        builder.Property(m => m.Counterparty).HasMaxLength(128);
        builder.Property(m => m.Notes).HasMaxLength(512);
        builder.HasIndex(m => new { m.TenantId, m.MovedOn });
        builder.HasOne(m => m.Item).WithMany().HasForeignKey(m => m.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
