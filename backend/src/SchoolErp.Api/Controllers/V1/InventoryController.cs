using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Inventory;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>School store: item catalogue and the stock register.</summary>
[ApiController]
[RequiresModule(TenantModules.Inventory)]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly ISender _sender;

    public InventoryController(ISender sender) => _sender = sender;

    /// <summary>The catalogue, optionally only items at or below reorder level.</summary>
    [HttpGet("items")]
    [HasPermission(Permissions.Inventory.View)]
    [ProducesResponseType(typeof(IReadOnlyList<InventoryItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems(
        [FromQuery] string? search, [FromQuery] bool lowOnly, CancellationToken ct) =>
        Ok(await _sender.Send(new GetInventoryItemsQuery(search, lowOnly), ct));

    /// <summary>Adds an item; stock starts at zero.</summary>
    [HttpPost("items")]
    [HasPermission(Permissions.Inventory.Manage)]
    [ProducesResponseType(typeof(InventoryItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateItem(
        [FromBody] CreateInventoryItemCommand command, CancellationToken ct)
    {
        var item = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetItems), new { }, item);
    }

    /// <summary>Edits catalogue details (never the balance).</summary>
    [HttpPut("items/{itemId:guid}")]
    [HasPermission(Permissions.Inventory.Manage)]
    [ProducesResponseType(typeof(InventoryItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateItem(
        Guid itemId, [FromBody] UpdateInventoryItemCommand command, CancellationToken ct) =>
        Ok(await _sender.Send(command with { ItemId = itemId }, ct));

    /// <summary>The stock register, newest first.</summary>
    [HttpGet("movements")]
    [HasPermission(Permissions.Inventory.View)]
    [ProducesResponseType(typeof(IReadOnlyList<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovements(
        [FromQuery] Guid? itemId, [FromQuery] int take, CancellationToken ct) =>
        Ok(await _sender.Send(new GetStockMovementsQuery(itemId, take <= 0 ? 100 : take), ct));

    /// <summary>Records stock in or out.</summary>
    [HttpPost("movements")]
    [HasPermission(Permissions.Inventory.Manage)]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordMovement(
        [FromBody] RecordStockMovementCommand command, CancellationToken ct)
    {
        var movement = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetMovements), new { }, movement);
    }
}
