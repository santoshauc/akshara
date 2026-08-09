using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.FrontOffice;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>The front desk: gate visitor register and student gate passes.</summary>
[ApiController]
[RequiresModule(TenantModules.FrontOffice)]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/front-office")]
public sealed class FrontOfficeController : ControllerBase
{
    private readonly ISender _sender;

    public FrontOfficeController(ISender sender) => _sender = sender;

    /// <summary>A day's gate register, or everyone still on the premises.</summary>
    [HttpGet("visitors")]
    [HasPermission(Permissions.FrontOffice.View)]
    [ProducesResponseType(typeof(IReadOnlyList<VisitorEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVisitors(
        [FromQuery] DateOnly? date, [FromQuery] bool openOnly, CancellationToken ct) =>
        Ok(await _sender.Send(new GetVisitorsQuery(date, openOnly), ct));

    /// <summary>Signs a visitor in and issues a badge number.</summary>
    [HttpPost("visitors")]
    [HasPermission(Permissions.FrontOffice.Manage)]
    [ProducesResponseType(typeof(VisitorEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckInVisitor(
        [FromBody] CheckInVisitorCommand command, CancellationToken ct)
    {
        var entry = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetVisitors), new { }, entry);
    }

    /// <summary>Signs a visitor out.</summary>
    [HttpPost("visitors/{visitorEntryId:guid}/check-out")]
    [HasPermission(Permissions.FrontOffice.Manage)]
    [ProducesResponseType(typeof(VisitorEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckOutVisitor(Guid visitorEntryId, CancellationToken ct) =>
        Ok(await _sender.Send(new CheckOutVisitorCommand(visitorEntryId), ct));

    /// <summary>Gate passes issued on a day (defaults to today).</summary>
    [HttpGet("gate-passes")]
    [HasPermission(Permissions.FrontOffice.View)]
    [ProducesResponseType(typeof(IReadOnlyList<GatePassDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGatePasses(
        [FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok(await _sender.Send(new GetGatePassesQuery(date), ct));

    /// <summary>Releases a student early; the primary guardian is notified.</summary>
    [HttpPost("gate-passes")]
    [HasPermission(Permissions.FrontOffice.Manage)]
    [ProducesResponseType(typeof(GatePassDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IssueGatePass(
        [FromBody] IssueGatePassCommand command, CancellationToken ct)
    {
        var pass = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetGatePasses), new { }, pass);
    }

    /// <summary>Marks the student back on the premises.</summary>
    [HttpPost("gate-passes/{gatePassId:guid}/returned")]
    [HasPermission(Permissions.FrontOffice.Manage)]
    [ProducesResponseType(typeof(GatePassDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkReturned(Guid gatePassId, CancellationToken ct) =>
        Ok(await _sender.Send(new MarkGatePassReturnedCommand(gatePassId), ct));
}
