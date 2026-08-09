using SchoolErp.Domain.Inventory;

namespace SchoolErp.AdminPortal.Models;

/// <summary>Catalogue row (mirrors InventoryItemDto).</summary>
public sealed record InventoryItemDto(
    Guid Id,
    string Name,
    string? Category,
    string Unit,
    int QuantityOnHand,
    int ReorderLevel,
    decimal? UnitCost,
    bool IsActive,
    bool IsLow);

/// <summary>Register line (mirrors StockMovementDto).</summary>
public sealed record StockMovementDto(
    Guid Id,
    Guid InventoryItemId,
    string ItemName,
    StockMovementKind Kind,
    int Quantity,
    int BalanceAfter,
    string? Counterparty,
    string? Notes,
    DateOnly MovedOn);

/// <summary>New-item payload.</summary>
public sealed record CreateInventoryItemRequest(
    string Name, string? Category, string Unit, int ReorderLevel, decimal? UnitCost);

/// <summary>Stock movement payload.</summary>
public sealed record RecordStockMovementRequest(
    Guid ItemId,
    StockMovementKind Kind,
    int Quantity,
    string? Counterparty,
    string? Notes,
    DateOnly? MovedOn);
