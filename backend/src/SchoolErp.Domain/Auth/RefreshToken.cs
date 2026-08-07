using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Auth;

/// <summary>
/// A single-use, rotating refresh token. Only the SHA-256 hash is stored; the
/// raw value exists solely in the client. Platform-scoped (not a
/// <see cref="TenantEntity"/>) because refresh happens before a tenant scope is
/// re-established — rows are only ever reached by exact hash lookup.
/// </summary>
public class RefreshToken : AuditableEntity
{
    /// <summary>Owning user (ASP.NET Identity user id).</summary>
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash (Base64) of the raw token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public string? CreatedByIp { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedByIp { get; set; }

    /// <summary>Hash of the token that replaced this one during rotation.</summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>Why the token was revoked (rotated, logout, reuse-detected…).</summary>
    public string? RevocationReason { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;
}
