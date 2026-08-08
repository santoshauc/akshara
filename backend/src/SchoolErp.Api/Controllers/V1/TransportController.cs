using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Application.Transport;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Transport: vehicles, routes, stops, student allocation.</summary>
[ApiController]
[RequiresModule(TenantModules.Transport)]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/transport")]
public sealed class TransportController : ControllerBase
{
    private readonly ISender _sender;

    public TransportController(ISender sender) => _sender = sender;

    /// <summary>Lists vehicles.</summary>
    [HttpGet("vehicles")]
    [HasPermission(Permissions.Transport.View)]
    [ProducesResponseType(typeof(IReadOnlyList<VehicleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVehicles(CancellationToken ct) =>
        Ok(await _sender.Send(new GetVehiclesQuery(), ct));

    /// <summary>Registers a vehicle.</summary>
    [HttpPost("vehicles")]
    [HasPermission(Permissions.Transport.Manage)]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateVehicle(
        [FromBody] CreateVehicleCommand command, CancellationToken ct)
    {
        var vehicle = await _sender.Send(command, ct);
        return Created($"api/v1/transport/vehicles/{vehicle.Id}", vehicle);
    }

    /// <summary>Lists routes with stops and rider counts.</summary>
    [HttpGet("routes")]
    [HasPermission(Permissions.Transport.View)]
    [ProducesResponseType(typeof(IReadOnlyList<TransportRouteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoutes(CancellationToken ct) =>
        Ok(await _sender.Send(new GetRoutesQuery(), ct));

    /// <summary>Creates a route with its ordered stops.</summary>
    [HttpPost("routes")]
    [HasPermission(Permissions.Transport.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRoute(
        [FromBody] CreateRouteCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return Created($"api/v1/transport/routes/{id}", id);
    }

    /// <summary>Allocates (or moves) a student to a route stop.</summary>
    [HttpPut("assignments")]
    [HasPermission(Permissions.Transport.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignStudent(
        [FromBody] AssignStudentTransportCommand command, CancellationToken ct)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }
}
