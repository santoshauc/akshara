using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Audit;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>The action audit trail (who did what, when, from where).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/audit")]
public sealed class AuditController : ControllerBase
{
    private readonly ISender _sender;

    public AuditController(ISender sender) => _sender = sender;

    /// <summary>Latest audit entries for the caller's scope (max 200).</summary>
    [HttpGet]
    [HasPermission(Permissions.Audit.View)]
    [ProducesResponseType(typeof(IReadOnlyList<AuditEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrail(
        [FromQuery] string? search,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        Ok(await _sender.Send(new GetAuditTrailQuery(search, from, to), ct));
}
