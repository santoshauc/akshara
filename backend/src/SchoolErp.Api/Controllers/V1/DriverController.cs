using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Transport;
using SchoolErp.Domain.Transport;
using SchoolErp.Infrastructure.Identity;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>
/// The driver app's API. Like the parent API, no permission claims — an
/// authenticated tenant user matched to a route (by linked user id or
/// verified phone) can only ever see and act on that route.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/driver")]
[Authorize]
public sealed class DriverController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;

    public DriverController(
        ISender sender, ICurrentUser currentUser, UserManager<ApplicationUser> userManager)
    {
        _sender = sender;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    /// <summary>The driver's route, stops, manifest and any active trip.</summary>
    [HttpGet("route")]
    [ProducesResponseType(typeof(DriverRouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoute(CancellationToken ct) =>
        Ok(await _sender.Send(new GetDriverRouteQuery(_currentUser.UserId, await PhoneAsync()), ct));

    /// <summary>Starts a trip (inspection checklist required).</summary>
    [HttpPost("trips")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartTrip([FromBody] StartTripRequest request, CancellationToken ct)
    {
        var id = await _sender.Send(new StartTripCommand(
            _currentUser.UserId, await PhoneAsync(),
            request.Type, request.InspectionOk, request.InspectionNotes), ct);
        return Created($"api/v1/driver/trips/{id}", id);
    }

    /// <summary>Records a GPS ping for the active trip.</summary>
    [HttpPost("location")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordLocation(
        [FromBody] LocationRequest request, CancellationToken ct)
    {
        await _sender.Send(new RecordLocationCommand(
            _currentUser.UserId, await PhoneAsync(), request.Latitude, request.Longitude), ct);
        return NoContent();
    }

    /// <summary>Marks a rider boarded/dropped/absent (notifies the guardian).</summary>
    [HttpPost("riders/{studentId:guid}/events")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRider(
        Guid studentId, [FromBody] RiderEventRequest request, CancellationToken ct)
    {
        await _sender.Send(new MarkRiderEventCommand(
            _currentUser.UserId, await PhoneAsync(), studentId, request.EventType, request.Remarks), ct);
        return NoContent();
    }

    /// <summary>Ends the active trip.</summary>
    [HttpPost("trips/end")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EndTrip(CancellationToken ct)
    {
        await _sender.Send(new EndTripCommand(_currentUser.UserId, await PhoneAsync()), ct);
        return NoContent();
    }

    private async Task<string?> PhoneAsync()
    {
        if (_currentUser.UserId is not { } userId)
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(userId);
        return user?.PhoneNumber;
    }
}

/// <summary>Trip-start payload.</summary>
public sealed record StartTripRequest(TripType Type, bool InspectionOk, string? InspectionNotes);

/// <summary>GPS ping payload.</summary>
public sealed record LocationRequest(decimal Latitude, decimal Longitude);

/// <summary>Rider event payload.</summary>
public sealed record RiderEventRequest(TripStudentEventType EventType, string? Remarks);
