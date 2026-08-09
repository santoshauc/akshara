using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Inventory;

/// <summary>Why stock moved. Receipts add; issues and write-offs remove.</summary>
public enum StockMovementKind
{
    /// <summary>Bought or donated into the store.</summary>
    Receipt = 1,

    /// <summary>Handed out to a student, teacher or department.</summary>
    Issue = 2,

    /// <summary>Damaged, lost or expired.</summary>
    WriteOff = 3,

    /// <summary>Correction after a physical count.</summary>
    Adjustment = 4,
}

/// <summary>
/// Something the school keeps in stock — uniforms, books, lab consumables,
/// sports kit. <see cref="QuantityOnHand"/> is maintained by movements so the
/// store register never needs a sum over history to answer "how many left".
/// </summary>
public class InventoryItem : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Shelf/category label, free text (e.g. "Uniform", "Lab").</summary>
    public string? Category { get; set; }

    /// <summary>Unit of issue: "piece", "set", "box", "litre".</summary>
    public string Unit { get; set; } = "piece";

    /// <summary>Running balance; never written directly outside movements.</summary>
    public int QuantityOnHand { get; set; }

    /// <summary>At or below this, the store shows the item as low.</summary>
    public int ReorderLevel { get; set; }

    /// <summary>Last known purchase price per unit, for valuation.</summary>
    public decimal? UnitCost { get; set; }

    /// <summary>Retired items stay for history but take no new movements.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// One movement in or out of the store. The register is append-only: a
/// mistaken issue is corrected with an Adjustment, never by editing history.
/// </summary>
public class StockMovement : TenantEntity
{
    public Guid InventoryItemId { get; set; }

    public InventoryItem? Item { get; set; }

    public StockMovementKind Kind { get; set; }

    /// <summary>Always positive; the kind decides the sign applied to stock.</summary>
    public int Quantity { get; set; }

    /// <summary>Balance immediately after this movement — the audit trail.</summary>
    public int BalanceAfter { get; set; }

    /// <summary>Who or what it went to/came from, free text.</summary>
    public string? Counterparty { get; set; }

    public string? Notes { get; set; }

    public DateOnly MovedOn { get; set; }
}
