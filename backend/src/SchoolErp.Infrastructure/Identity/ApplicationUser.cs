using Microsoft.AspNetCore.Identity;

namespace SchoolErp.Infrastructure.Identity;

/// <summary>
/// Platform user account. <see cref="TenantId"/> is null only for platform
/// operators (Super Admin); every school user belongs to exactly one tenant.
/// <c>UserName</c> is an opaque unique key — login happens by
/// (tenant, email/phone), never by user name.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Owning school, or null for platform operators.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Display name shown across the portal and apps.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Deactivated users cannot authenticate but are retained for audit.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Role scoped to a tenant (null tenant = platform role). Permissions are
/// attached as role claims of type <c>permission</c>.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    /// <summary>Owning school, or null for platform roles.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Human description shown in the role editor.</summary>
    public string? Description { get; set; }
}
