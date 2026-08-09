using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Insights;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Analytics for school management.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/insights")]
public sealed class InsightsController : ControllerBase
{
    private readonly ISender _sender;

    public InsightsController(ISender sender) => _sender = sender;

    /// <summary>The management dashboard's chart data, tenant-scoped.</summary>
    [HttpGet("management")]
    [HasPermission(Permissions.Insights.View)]
    [ProducesResponseType(typeof(ManagementInsightsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetManagement(CancellationToken ct) =>
        Ok(await _sender.Send(new GetManagementInsightsQuery(), ct));
}
