using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Leave;
using SchoolErp.Domain.Leave;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>
/// Leave requests: the staff inbox with approve/reject, plus self-service
/// endpoints any signed-in staff member can use for their own leave.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/leave")]
public sealed class LeaveController : ControllerBase
{
    private readonly ISender _sender;

    public LeaveController(ISender sender) => _sender = sender;

    /// <summary>All requests, newest first; optional status filter.</summary>
    [HttpGet]
    [HasPermission(Permissions.Leave.View)]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRequests(
        [FromQuery] LeaveRequestStatus? status, CancellationToken ct) =>
        Ok(await _sender.Send(new GetLeaveRequestsQuery(status), ct));

    /// <summary>
    /// Decides a pending request. Approving a student request marks the
    /// range as Leave in attendance.
    /// </summary>
    [HttpPost("{id:guid}/decision")]
    [HasPermission(Permissions.Leave.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Decide(
        Guid id, [FromBody] LeaveDecisionRequest request, CancellationToken ct)
    {
        await _sender.Send(
            new DecideLeaveRequestCommand(id, request.Approve, request.Note), ct);
        return NoContent();
    }

    /// <summary>
    /// The caller's own leave requests (any signed-in staff).
    ///
    /// [Authorize] rather than [HasPermission]: these two are the self-service
    /// pair, open to every staff member regardless of role, so they demand a
    /// signed-in caller and nothing more. It has to be stated explicitly — the
    /// API has no fallback authorization policy, so an action with no attribute
    /// at all is served to anybody. That is what happened here: this endpoint
    /// answered 200 with an empty list to anonymous callers, and only looked
    /// harmless because the handler found no user to match on.
    /// </summary>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken ct) =>
        Ok(await _sender.Send(new GetMyLeaveRequestsQuery(), ct));

    /// <summary>Files the caller's own leave request (any signed-in staff).</summary>
    [HttpPost("mine")]
    [Authorize]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitMine(
        [FromBody] StaffLeaveRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new SubmitLeaveRequestCommand(
            null, request.FromDate, request.ToDate, request.Reason), ct));
}

/// <summary>Approve/reject payload.</summary>
public sealed record LeaveDecisionRequest(bool Approve, string? Note);

/// <summary>Staff self-service leave payload.</summary>
public sealed record StaffLeaveRequest(DateOnly FromDate, DateOnly ToDate, string Reason);
