using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Platform;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>
/// The platform's own dashboard: every school at once. Separate from
/// <c>DashboardController</c>, which is a single school's day and which the
/// tenant guard rightly refuses to a platform account.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/platform/dashboard")]
[PlatformOnly]
public sealed class PlatformDashboardController : ControllerBase
{
    private readonly ISender _sender;

    public PlatformDashboardController(ISender sender) => _sender = sender;

    /// <summary>Platform figures over a trailing window (default 30 days).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PlatformDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int windowDays = 30, CancellationToken ct = default) =>
        Ok(await _sender.Send(new GetPlatformDashboardQuery(windowDays), ct));
}
