using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Timetable;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Class timetables: define, publish, view.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/timetable")]
public sealed class TimetableController : ControllerBase
{
    private readonly ISender _sender;

    public TimetableController(ISender sender) => _sender = sender;

    /// <summary>The timetable for a class scope (drafts included — staff view).</summary>
    [HttpGet]
    [HasPermission(Permissions.Timetable.View)]
    [ProducesResponseType(typeof(IReadOnlyList<TimetableEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimetable(
        [FromQuery] Guid classId, [FromQuery] Guid? sectionId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetTimetableQuery(classId, sectionId), ct));

    /// <summary>Replaces the timetable for a class scope (entries start unpublished).</summary>
    [HttpPut]
    [HasPermission(Permissions.Timetable.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DefineTimetable(
        [FromBody] DefineTimetableCommand command, CancellationToken ct)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Publishes the scope's timetable to parents.</summary>
    [HttpPost("publish")]
    [HasPermission(Permissions.Timetable.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(
        [FromBody] PublishTimetableCommand command, CancellationToken ct)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }
}
