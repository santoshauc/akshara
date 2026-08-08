using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Users;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Staff accounts and roles of the current school.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/users")]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender) => _sender = sender;

    /// <summary>Staff accounts (users holding at least one role).</summary>
    [HttpGet]
    [HasPermission(Permissions.Users.View)]
    [ProducesResponseType(typeof(IReadOnlyList<StaffUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] string? search, CancellationToken ct) =>
        Ok(await _sender.Send(new GetStaffUsersQuery(search), ct));

    /// <summary>Creates a staff account with a temporary password.</summary>
    [HttpPost]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateStaffUserCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetUsers), new { }, id);
    }

    /// <summary>Edits name, active state and role assignments.</summary>
    [HttpPut("{userId:guid}")]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateUser(
        Guid userId, [FromBody] UpdateStaffUserCommand command, CancellationToken ct)
    {
        await _sender.Send(command with { UserId = userId }, ct);
        return NoContent();
    }

    /// <summary>Admin-set temporary password (revokes the user's sessions).</summary>
    [HttpPost("{userId:guid}/reset-password")]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(
        Guid userId, [FromBody] ResetUserPasswordCommand command, CancellationToken ct)
    {
        await _sender.Send(command with { UserId = userId }, ct);
        return NoContent();
    }

    /// <summary>Roles of the school with their permission bundles.</summary>
    [HttpGet("roles")]
    [HasPermission(Permissions.Users.View)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(CancellationToken ct) =>
        Ok(await _sender.Send(new GetRolesQuery(), ct));

    /// <summary>The full permission catalog (for the role editor).</summary>
    [HttpGet("permissions")]
    [HasPermission(Permissions.Users.View)]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public IActionResult GetPermissionCatalog() => Ok(Permissions.All);

    /// <summary>Creates a role as a permission bundle.</summary>
    [HttpPost("roles")]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRole(
        [FromBody] CreateRoleCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetRoles), new { }, id);
    }

    /// <summary>Replaces a role's description and permission set.</summary>
    [HttpPut("roles/{roleId:guid}")]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRole(
        Guid roleId, [FromBody] UpdateRoleCommand command, CancellationToken ct)
    {
        await _sender.Send(command with { RoleId = roleId }, ct);
        return NoContent();
    }
}
