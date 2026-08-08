namespace SchoolErp.AdminPortal.Models;

/// <summary>Staff account row (mirrors StaffUserDto).</summary>
public sealed record StaffUserDto(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    List<string> Roles,
    bool IsActive);

/// <summary>Role row (mirrors RoleDto).</summary>
public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    List<string> Permissions,
    bool IsSystem);

/// <summary>Create payload (mirrors CreateStaffUserCommand).</summary>
public sealed record CreateStaffUserRequest(
    string FullName,
    string? Email,
    string? Phone,
    string TemporaryPassword,
    List<string> Roles);

/// <summary>Edit payload (mirrors UpdateStaffUserCommand; id from route).</summary>
public sealed record UpdateStaffUserRequest(
    Guid UserId,
    string FullName,
    bool IsActive,
    List<string> Roles);

/// <summary>Create-role payload (mirrors CreateRoleCommand).</summary>
public sealed record CreateRoleRequest(string Name, string? Description, List<string> Permissions);

/// <summary>Update-role payload (mirrors UpdateRoleCommand; id from route).</summary>
public sealed record UpdateRoleRequest(Guid RoleId, string? Description, List<string> Permissions);
