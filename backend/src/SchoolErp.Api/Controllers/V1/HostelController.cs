using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Application.Hostel;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Hostels: buildings, rooms, and student stays.</summary>
[ApiController]
[RequiresModule(TenantModules.Hostel)]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/hostel")]
public sealed class HostelController : ControllerBase
{
    private readonly ISender _sender;

    public HostelController(ISender sender) => _sender = sender;

    /// <summary>Hostels with room and occupancy tallies.</summary>
    [HttpGet]
    [HasPermission(Permissions.Hostel.View)]
    [ProducesResponseType(typeof(IReadOnlyList<HostelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHostels(CancellationToken ct) =>
        Ok(await _sender.Send(new GetHostelsQuery(), ct));

    /// <summary>Creates a hostel building.</summary>
    [HttpPost]
    [HasPermission(Permissions.Hostel.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateHostel(
        [FromBody] CreateHostelCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetHostels), new { }, id);
    }

    /// <summary>Rooms of one hostel with live occupancy.</summary>
    [HttpGet("{hostelId:guid}/rooms")]
    [HasPermission(Permissions.Hostel.View)]
    [ProducesResponseType(typeof(IReadOnlyList<HostelRoomDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRooms(Guid hostelId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetHostelRoomsQuery(hostelId), ct));

    /// <summary>Adds a room to a hostel.</summary>
    [HttpPost("{hostelId:guid}/rooms")]
    [HasPermission(Permissions.Hostel.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddRoom(
        Guid hostelId, [FromBody] AddRoomRequest request, CancellationToken ct)
    {
        var id = await _sender.Send(
            new AddHostelRoomCommand(hostelId, request.RoomNumber, request.Capacity), ct);
        return CreatedAtAction(nameof(GetRooms), new { hostelId }, id);
    }

    /// <summary>Open stays (whole school or one room).</summary>
    [HttpGet("allocations")]
    [HasPermission(Permissions.Hostel.View)]
    [ProducesResponseType(typeof(IReadOnlyList<HostelAllocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllocations(
        [FromQuery] Guid? roomId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetHostelAllocationsQuery(roomId), ct));

    /// <summary>Moves a student into a room.</summary>
    [HttpPost("allocations")]
    [HasPermission(Permissions.Hostel.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Allocate(
        [FromBody] AllocateHostelRoomCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetAllocations), new { }, id);
    }

    /// <summary>Ends a stay.</summary>
    [HttpPost("allocations/{allocationId:guid}/vacate")]
    [HasPermission(Permissions.Hostel.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Vacate(Guid allocationId, CancellationToken ct)
    {
        await _sender.Send(new VacateHostelRoomCommand(allocationId), ct);
        return NoContent();
    }
}

/// <summary>Room payload (hostel id comes from the route).</summary>
public sealed record AddRoomRequest(string RoomNumber, int Capacity);
