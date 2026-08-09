namespace SchoolErp.AdminPortal.Models;

/// <summary>Audit trail row (mirrors AuditEventDto).</summary>
public sealed record AuditEventDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Action,
    string? UserName,
    string? UserId,
    string? IpAddress,
    string? SchoolName = null);

/// <summary>Platform operator account (mirrors PlatformOperatorDto).</summary>
public sealed record PlatformOperatorDto(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    bool IsActive,
    bool MfaEnabled,
    DateTimeOffset CreatedAt);
