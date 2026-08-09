using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Inventory;

namespace SchoolErp.Application.Inventory;

/// <summary>A stocked item with its current balance.</summary>
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

/// <summary>One line of the store register.</summary>
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

/// <summary>Adds an item to the store catalogue.</summary>
public sealed record CreateInventoryItemCommand(
    string Name,
    string? Category,
    string Unit,
    int ReorderLevel,
    decimal? UnitCost) : IRequest<InventoryItemDto>;

/// <summary>Edits catalogue details. Stock is never set here — use movements.</summary>
public sealed record UpdateInventoryItemCommand(
    Guid ItemId,
    string Name,
    string? Category,
    string Unit,
    int ReorderLevel,
    decimal? UnitCost,
    bool IsActive) : IRequest<InventoryItemDto>;

/// <summary>Records stock in or out and moves the running balance.</summary>
public sealed record RecordStockMovementCommand(
    Guid ItemId,
    StockMovementKind Kind,
    int Quantity,
    string? Counterparty,
    string? Notes,
    DateOnly? MovedOn) : IRequest<StockMovementDto>;

/// <summary>The catalogue, optionally only what needs reordering.</summary>
public sealed record GetInventoryItemsQuery(string? Search, bool LowOnly = false)
    : IRequest<IReadOnlyList<InventoryItemDto>>;

/// <summary>Recent movements, newest first, optionally for one item.</summary>
public sealed record GetStockMovementsQuery(Guid? ItemId, int Take = 100)
    : IRequest<IReadOnlyList<StockMovementDto>>;

/// <summary>Shape rules for the catalogue.</summary>
public sealed class CreateInventoryItemCommandValidator
    : AbstractValidator<CreateInventoryItemCommand>
{
    public CreateInventoryItemCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Category).MaximumLength(64);
        RuleFor(c => c.Unit).NotEmpty().MaximumLength(16);
        RuleFor(c => c.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(c => c.UnitCost).GreaterThanOrEqualTo(0).When(c => c.UnitCost.HasValue);
    }
}

/// <summary>Same rules on edit.</summary>
public sealed class UpdateInventoryItemCommandValidator
    : AbstractValidator<UpdateInventoryItemCommand>
{
    public UpdateInventoryItemCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Category).MaximumLength(64);
        RuleFor(c => c.Unit).NotEmpty().MaximumLength(16);
        RuleFor(c => c.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(c => c.UnitCost).GreaterThanOrEqualTo(0).When(c => c.UnitCost.HasValue);
    }
}

/// <summary>Movement rules; the stock floor is enforced in the handler.</summary>
public sealed class RecordStockMovementCommandValidator
    : AbstractValidator<RecordStockMovementCommand>
{
    public RecordStockMovementCommandValidator()
    {
        RuleFor(c => c.Kind).IsInEnum();
        RuleFor(c => c.Quantity).GreaterThan(0)
            .WithMessage("Quantity is always positive; the movement kind decides the direction.");
        RuleFor(c => c.Counterparty).MaximumLength(128);
        RuleFor(c => c.Notes).MaximumLength(512);
    }
}

/// <summary>Creates the catalogue row (stock starts at zero).</summary>
public sealed class CreateInventoryItemCommandHandler
    : IRequestHandler<CreateInventoryItemCommand, InventoryItemDto>
{
    private readonly IApplicationDbContext _db;

    public CreateInventoryItemCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<InventoryItemDto> Handle(
        CreateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _db.InventoryItems.AnyAsync(i => i.Name == name, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ConflictException($"An item named '{name}' already exists.");
        }

        var item = new InventoryItem
        {
            Name = name,
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            Unit = request.Unit.Trim(),
            ReorderLevel = request.ReorderLevel,
            UnitCost = request.UnitCost,
            QuantityOnHand = 0,
        };
        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return InventoryMappings.ToDto(item);
    }
}

/// <summary>Applies catalogue edits.</summary>
public sealed class UpdateInventoryItemCommandHandler
    : IRequestHandler<UpdateInventoryItemCommand, InventoryItemDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateInventoryItemCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<InventoryItemDto> Handle(
        UpdateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Inventory item", request.ItemId);

        var name = request.Name.Trim();
        if (name != item.Name &&
            await _db.InventoryItems.AnyAsync(
                i => i.Id != item.Id && i.Name == name, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"An item named '{name}' already exists.");
        }

        item.Name = name;
        item.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        item.Unit = request.Unit.Trim();
        item.ReorderLevel = request.ReorderLevel;
        item.UnitCost = request.UnitCost;
        item.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return InventoryMappings.ToDto(item);
    }
}

/// <summary>Moves stock and writes the register line.</summary>
public sealed class RecordStockMovementCommandHandler
    : IRequestHandler<RecordStockMovementCommand, StockMovementDto>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public RecordStockMovementCommandHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<StockMovementDto> Handle(
        RecordStockMovementCommand request, CancellationToken cancellationToken)
    {
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Inventory item", request.ItemId);

        if (!item.IsActive)
        {
            throw new ConflictException($"'{item.Name}' is retired and takes no new movements.");
        }

        // Adjustments set an absolute count; everything else is a delta.
        var newBalance = request.Kind switch
        {
            StockMovementKind.Receipt => item.QuantityOnHand + request.Quantity,
            StockMovementKind.Issue => item.QuantityOnHand - request.Quantity,
            StockMovementKind.WriteOff => item.QuantityOnHand - request.Quantity,
            _ => request.Quantity,
        };

        if (newBalance < 0)
        {
            throw new ConflictException(
                $"Only {item.QuantityOnHand} {item.Unit} of '{item.Name}' in stock.");
        }

        var movement = new StockMovement
        {
            InventoryItemId = item.Id,
            Kind = request.Kind,
            Quantity = request.Quantity,
            BalanceAfter = newBalance,
            Counterparty = string.IsNullOrWhiteSpace(request.Counterparty)
                ? null
                : request.Counterparty.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            MovedOn = request.MovedOn ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime),
        };
        item.QuantityOnHand = newBalance;
        _db.StockMovements.Add(movement);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new StockMovementDto(
            movement.Id, item.Id, item.Name, movement.Kind, movement.Quantity,
            movement.BalanceAfter, movement.Counterparty, movement.Notes, movement.MovedOn);
    }
}

/// <summary>Reads the catalogue.</summary>
public sealed class GetInventoryItemsQueryHandler
    : IRequestHandler<GetInventoryItemsQuery, IReadOnlyList<InventoryItemDto>>
{
    private readonly IApplicationDbContext _db;

    public GetInventoryItemsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<InventoryItemDto>> Handle(
        GetInventoryItemsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.InventoryItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(i =>
                EF.Functions.ILike(i.Name, $"%{term}%") ||
                (i.Category != null && EF.Functions.ILike(i.Category, $"%{term}%")));
        }

        if (request.LowOnly)
        {
            query = query.Where(i => i.IsActive && i.QuantityOnHand <= i.ReorderLevel);
        }

        var items = await query
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return items.Select(InventoryMappings.ToDto).ToList();
    }
}

/// <summary>Reads the register.</summary>
public sealed class GetStockMovementsQueryHandler
    : IRequestHandler<GetStockMovementsQuery, IReadOnlyList<StockMovementDto>>
{
    private readonly IApplicationDbContext _db;

    public GetStockMovementsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<StockMovementDto>> Handle(
        GetStockMovementsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.StockMovements.AsNoTracking();
        if (request.ItemId is { } itemId)
        {
            query = query.Where(m => m.InventoryItemId == itemId);
        }

        return await query
            .OrderByDescending(m => m.MovedOn).ThenByDescending(m => m.CreatedAt)
            .Take(Math.Clamp(request.Take, 1, 500))
            .Select(m => new StockMovementDto(
                m.Id, m.InventoryItemId, m.Item!.Name, m.Kind, m.Quantity,
                m.BalanceAfter, m.Counterparty, m.Notes, m.MovedOn))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>Hand-written mappings (no AutoMapper in this repo).</summary>
internal static class InventoryMappings
{
    public static InventoryItemDto ToDto(InventoryItem item) =>
        new(item.Id, item.Name, item.Category, item.Unit, item.QuantityOnHand,
            item.ReorderLevel, item.UnitCost, item.IsActive,
            item.IsActive && item.QuantityOnHand <= item.ReorderLevel);
}
