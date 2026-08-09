using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Dashboard;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>The staff landing-page numbers, scoped to the caller's school.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly ISender _sender;

    public DashboardController(ISender sender) => _sender = sender;

    /// <summary>Today's tiles: attendance, fees, loans, leave, messages, exams.</summary>
    [HttpGet]
    [HasPermission(Permissions.Students.View)]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _sender.Send(new GetDashboardQuery(), ct));
}
