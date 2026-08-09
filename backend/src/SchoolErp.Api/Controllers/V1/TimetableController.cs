using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Application.Timetable;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Class timetables: define, publish, view.</summary>
[ApiController]
[RequiresModule(TenantModules.Timetable)]
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

    /// <summary>The cover plan for an absent teacher on a date.</summary>
    [HttpGet("substitutions/plan")]
    [HasPermission(Permissions.Timetable.Manage)]
    [ProducesResponseType(typeof(IReadOnlyList<SubstitutionSlotDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubstitutionPlan(
        [FromQuery] Guid teacherId, [FromQuery] DateOnly date, CancellationToken ct) =>
        Ok(await _sender.Send(new GetSubstitutionPlanQuery(teacherId, date), ct));

    /// <summary>Publishes (upserts) the day's substitutions.</summary>
    [HttpPost("substitutions")]
    [HasPermission(Permissions.Timetable.Manage)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApplySubstitutions(
        [FromBody] ApplySubstitutionsCommand command, CancellationToken ct) =>
        Ok(await _sender.Send(command, ct));

    /// <summary>The cover list for a date.</summary>
    [HttpGet("substitutions")]
    [HasPermission(Permissions.Timetable.View)]
    [ProducesResponseType(typeof(IReadOnlyList<SubstitutionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubstitutions(
        [FromQuery] DateOnly date, CancellationToken ct) =>
        Ok(await _sender.Send(new GetSubstitutionsQuery(date), ct));
}
