using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Staff;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Teaching staff directory and per-teacher schedules.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/teachers")]
public sealed class TeachersController : ControllerBase
{
    private readonly ISender _sender;

    public TeachersController(ISender sender) => _sender = sender;

    /// <summary>Teacher directory, optionally filtered by name/code/phone.</summary>
    [HttpGet]
    [HasPermission(Permissions.Staff.View)]
    [ProducesResponseType(typeof(IReadOnlyList<TeacherDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeachers([FromQuery] string? search, CancellationToken ct) =>
        Ok(await _sender.Send(new GetTeachersQuery(search), ct));

    /// <summary>Registers a teacher.</summary>
    [HttpPost]
    [HasPermission(Permissions.Staff.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTeacher(
        [FromBody] CreateTeacherCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetTeachers), new { }, id);
    }

    /// <summary>Edits a teacher (including active state).</summary>
    [HttpPut("{teacherId:guid}")]
    [HasPermission(Permissions.Staff.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTeacher(
        Guid teacherId, [FromBody] UpdateTeacherCommand command, CancellationToken ct)
    {
        await _sender.Send(command with { TeacherId = teacherId }, ct);
        return NoContent();
    }

    /// <summary>
    /// One-click login for a teacher: creates a staff account with the
    /// school's "Teacher" role (attendance, marks, homework, timetable)
    /// from the teacher's own contact details. Returns the new user id.
    /// </summary>
    [HttpPost("{teacherId:guid}/login")]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLogin(
        Guid teacherId, [FromBody] CreateTeacherLoginRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(
            new CreateTeacherLoginCommand(teacherId, request.TemporaryPassword), ct));

    /// <summary>The teacher's weekly schedule across all classes (drafts included).</summary>
    [HttpGet("{teacherId:guid}/schedule")]
    [HasPermission(Permissions.Staff.View)]
    [ProducesResponseType(typeof(IReadOnlyList<TeacherScheduleItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSchedule(Guid teacherId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetTeacherScheduleQuery(teacherId), ct));

    /// <summary>
    /// The signed-in teacher's own day. Needs no staff permission — it reads
    /// nothing but the caller's own slots, and 404s if the caller has no
    /// linked teacher record.
    /// </summary>
    [HttpGet("me/day")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ProducesResponseType(typeof(MyTeacherDayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyDay(CancellationToken ct) =>
        Ok(await _sender.Send(new GetMyTeacherDayQuery(), ct));
}

/// <summary>Temporary password for a new teacher login.</summary>
public sealed record CreateTeacherLoginRequest(string TemporaryPassword);
