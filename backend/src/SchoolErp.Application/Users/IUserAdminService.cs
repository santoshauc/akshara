namespace SchoolErp.Application.Users;

/// <summary>A staff account as shown in Users &amp; roles.</summary>
public sealed record StaffUserDto(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    IReadOnlyList<string> Roles,
    bool IsActive);

/// <summary>A role as a named permission bundle. System roles are read-only.</summary>
public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions,
    bool IsSystem);

/// <summary>
/// Staff-account and role administration for the current school. Implemented
/// in Infrastructure over ASP.NET Identity; commands wrap it so validation
/// and the audit trail apply.
/// </summary>
public interface IUserAdminService
{
    /// <summary>Staff users (accounts holding at least one role) of the tenant.</summary>
    Task<IReadOnlyList<StaffUserDto>> GetUsersAsync(string? search, CancellationToken ct = default);

    /// <summary>Creates a staff account with a temporary password and roles.
    /// Throws ConflictException on duplicate email/phone within the school.</summary>
    Task<Guid> CreateUserAsync(
        string fullName, string? email, string? phone, string temporaryPassword,
        IReadOnlyList<string> roles, CancellationToken ct = default);

    /// <summary>Edits name/active state and replaces role assignments.
    /// Deactivation blocks login and revokes open sessions.</summary>
    Task UpdateUserAsync(
        Guid userId, string fullName, bool isActive, IReadOnlyList<string> roles,
        CancellationToken ct = default);

    /// <summary>Admin-set temporary password (also revokes open sessions).</summary>
    Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default);

    /// <summary>Roles of the tenant with their permission bundles.</summary>
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct = default);

    /// <summary>Creates a role with the given permission set.</summary>
    Task<Guid> CreateRoleAsync(
        string name, string? description, IReadOnlyList<string> permissions,
        CancellationToken ct = default);

    /// <summary>Replaces a role's description and permission set. System roles refuse.</summary>
    Task UpdateRoleAsync(
        Guid roleId, string? description, IReadOnlyList<string> permissions,
        CancellationToken ct = default);
}
