using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Academics;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Academic structure: sessions, classes and sections.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/academics")]
public sealed class AcademicsController : ControllerBase
{
    private readonly ISender _sender;

    public AcademicsController(ISender sender) => _sender = sender;

    /// <summary>Lists academic years, newest first.</summary>
    [HttpGet("years")]
    [HasPermission(Permissions.Academics.View)]
    [ProducesResponseType(typeof(IReadOnlyList<AcademicYearDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetYears(CancellationToken ct) =>
        Ok(await _sender.Send(new GetAcademicYearsQuery(), ct));

    /// <summary>Creates an academic year.</summary>
    [HttpPost("years")]
    [HasPermission(Permissions.Academics.Manage)]
    [ProducesResponseType(typeof(AcademicYearDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateYear(
        [FromBody] CreateAcademicYearCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return Created($"api/v1/academics/years/{result.Id}", result);
    }

    /// <summary>Marks a year as the current session.</summary>
    [HttpPost("years/{id:guid}/set-current")]
    [HasPermission(Permissions.Academics.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCurrentYear(Guid id, CancellationToken ct)
    {
        await _sender.Send(new SetCurrentAcademicYearCommand(id), ct);
        return NoContent();
    }

    /// <summary>Lists classes with their sections.</summary>
    [HttpGet("classes")]
    [HasPermission(Permissions.Academics.View)]
    [ProducesResponseType(typeof(IReadOnlyList<SchoolClassDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClasses(CancellationToken ct) =>
        Ok(await _sender.Send(new GetClassesQuery(), ct));

    /// <summary>Creates a class with its sections.</summary>
    [HttpPost("classes")]
    [HasPermission(Permissions.Academics.Manage)]
    [ProducesResponseType(typeof(SchoolClassDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateClass(
        [FromBody] CreateClassCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return Created($"api/v1/academics/classes/{result.Id}", result);
    }
}
