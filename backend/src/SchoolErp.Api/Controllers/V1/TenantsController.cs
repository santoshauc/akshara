using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.TenantCatalog;
using SchoolErp.Application.TenantCatalog.Commands;
using SchoolErp.Application.TenantCatalog.Queries;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Shared.Authorization;
using SchoolErp.Shared.Models;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Super Admin school-catalog management.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/tenants")]
public sealed class TenantsController : ControllerBase
{
    private readonly ISender _sender;

    public TenantsController(ISender sender) => _sender = sender;

    /// <summary>Lists schools with search, status filter and pagination.</summary>
    [HttpGet]
    [HasPermission(Permissions.TenantCatalog.View)]
    [ProducesResponseType(typeof(PagedResult<TenantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenants(
        [FromQuery] string? search,
        [FromQuery] TenantStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetTenantsQuery(search, status, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Gets one school by id.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.TenantCatalog.View)]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenant(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetTenantByIdQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Onboards a new school.</summary>
    [HttpPost]
    [HasPermission(Permissions.TenantCatalog.Manage)]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetTenant), new { id = result.Id, version = "1" }, result);
    }

    /// <summary>Updates a school's profile, branding and entitlements.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.TenantCatalog.Manage)]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTenant(
        Guid id, [FromBody] UpdateTenantCommand command, CancellationToken ct)
    {
        if (id != command.Id)
        {
            return BadRequest("Route id and body id do not match.");
        }

        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Activates, suspends or archives a school.</summary>
    [HttpPost("{id:guid}/status")]
    [HasPermission(Permissions.TenantCatalog.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(
        Guid id, [FromBody] ChangeStatusRequest request, CancellationToken ct)
    {
        await _sender.Send(new ChangeTenantStatusCommand(id, request.Status), ct);
        return NoContent();
    }
}

/// <summary>Status-change payload.</summary>
public sealed record ChangeStatusRequest(TenantStatus Status);
