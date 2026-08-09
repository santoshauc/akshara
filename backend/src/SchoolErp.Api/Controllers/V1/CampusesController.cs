using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Campuses;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>
/// The locations an institution operates from. Not gated by a subscription
/// module: a multi-campus trust needs this on every plan, and a single-campus
/// school simply has one row.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/campuses")]
public sealed class CampusesController : ControllerBase
{
    private readonly ISender _sender;

    public CampusesController(ISender sender) => _sender = sender;

    /// <summary>Campuses of the caller's institution, primary first.</summary>
    [HttpGet]
    [HasPermission(Permissions.Campuses.View)]
    [ProducesResponseType(typeof(IReadOnlyList<CampusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCampuses(
        [FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _sender.Send(new GetCampusesQuery(includeInactive), ct));

    [HttpPost]
    [HasPermission(Permissions.Campuses.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCampus(
        [FromBody] CreateCampusCommand command, CancellationToken ct) =>
        Ok(await _sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Campuses.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCampus(
        Guid id, [FromBody] UpdateCampusCommand command, CancellationToken ct)
    {
        await _sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    /// <summary>Makes this the head campus; the previous one steps down.</summary>
    [HttpPut("{id:guid}/primary")]
    [HasPermission(Permissions.Campuses.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetPrimary(Guid id, CancellationToken ct)
    {
        await _sender.Send(new SetPrimaryCampusCommand(id), ct);
        return NoContent();
    }
}
