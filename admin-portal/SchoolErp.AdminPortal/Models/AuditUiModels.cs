namespace SchoolErp.AdminPortal.Models;

/// <summary>Audit trail row (mirrors AuditEventDto).</summary>
public sealed record AuditEventDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Action,
    string? UserName,
    string? UserId,
    string? IpAddress);
