using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Academics;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>
/// A college's departments and the programmes they run. Guarded by the
/// ordinary academics permissions — this is academic structure, just the
/// higher-education shape of it. A school never calls these; its set is empty.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/departments")]
public sealed class DepartmentsController : ControllerBase
{
    private readonly ISender _sender;

    public DepartmentsController(ISender sender) => _sender = sender;

    /// <summary>Departments with their programmes, alphabetically.</summary>
    [HttpGet]
    [HasPermission(Permissions.Academics.View)]
    [ProducesResponseType(typeof(IReadOnlyList<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartments(
        [FromQuery] bool includeClosed, CancellationToken ct) =>
        Ok(await _sender.Send(new GetDepartmentsQuery(includeClosed), ct));

    [HttpPost]
    [HasPermission(Permissions.Academics.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDepartment(
        [FromBody] CreateDepartmentCommand command, CancellationToken ct) =>
        Ok(await _sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Academics.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateDepartment(
        Guid id, [FromBody] UpdateDepartmentCommand command, CancellationToken ct)
    {
        await _sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    [HttpPost("programmes")]
    [HasPermission(Permissions.Academics.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProgramme(
        [FromBody] CreateProgrammeCommand command, CancellationToken ct) =>
        Ok(await _sender.Send(command, ct));

    [HttpPut("programmes/{id:guid}")]
    [HasPermission(Permissions.Academics.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProgramme(
        Guid id, [FromBody] UpdateProgrammeCommand command, CancellationToken ct)
    {
        await _sender.Send(command with { Id = id }, ct);
        return NoContent();
    }
}
