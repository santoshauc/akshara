using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Attendance;
using SchoolErp.Application.Attendance.Commands;
using SchoolErp.Application.Attendance.Queries;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Daily attendance: marking grids and student calendars.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/attendance")]
public sealed class AttendanceController : ControllerBase
{
    private readonly ISender _sender;

    public AttendanceController(ISender sender) => _sender = sender;

    /// <summary>The marking grid for a section on a date (period optional).</summary>
    [HttpGet("sections/{sectionId:guid}")]
    [HasPermission(Permissions.Attendance.View)]
    [ProducesResponseType(typeof(SectionAttendanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSectionAttendance(
        Guid sectionId, [FromQuery] DateOnly date, [FromQuery] int? period,
        CancellationToken ct) =>
        Ok(await _sender.Send(new GetSectionAttendanceQuery(sectionId, date, period), ct));

    /// <summary>Marks (or re-marks) a section for a date (period optional).</summary>
    [HttpPost("sections/{sectionId:guid}")]
    [HasPermission(Permissions.Attendance.Mark)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkSectionAttendance(
        Guid sectionId, [FromBody] MarkAttendanceRequest request, CancellationToken ct)
    {
        await _sender.Send(new MarkAttendanceCommand(
            sectionId, request.Date, request.Entries, request.Period), ct);
        return NoContent();
    }

    /// <summary>A student's month calendar with counters.</summary>
    [HttpGet("students/{studentId:guid}")]
    [HasPermission(Permissions.Attendance.View)]
    [ProducesResponseType(typeof(StudentMonthAttendanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentMonth(
        Guid studentId, [FromQuery] int year, [FromQuery] int month, CancellationToken ct) =>
        Ok(await _sender.Send(new GetStudentMonthAttendanceQuery(studentId, year, month), ct));
}

/// <summary>Marking payload. Null period = daily roll call.</summary>
public sealed record MarkAttendanceRequest(
    DateOnly Date, IReadOnlyList<AttendanceEntry> Entries, int? Period = null);
