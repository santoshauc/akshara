using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Audit;

/// <summary>
/// One entry in the tamper-evident action trail: who did what, in which
/// school, when, from where. Deliberately NOT a <see cref="TenantEntity"/> —
/// platform-level actions (tenant onboarding, platform logins) have no tenant
/// scope, so the table has no RLS; the explicit nullable <see cref="TenantId"/>
/// is filtered in queries and school admins only ever see their own rows.
/// Rows are append-only: nothing in the application updates or deletes them.
/// </summary>
public class AuditEvent : AuditableEntity
{
    /// <summary>School the action belongs to; null for platform-level actions.</summary>
    public Guid? TenantId { get; set; }

    public string? UserId { get; set; }

    /// <summary>Display name captured at write time (survives user renames).</summary>
    public string? UserName { get; set; }

    /// <summary>The command that ran, e.g. "AdmitStudentCommand".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Optional human-readable context. Never contains secrets or payload PII.</summary>
    public string? Detail { get; set; }

    /// <summary>Caller IP as seen by the API, when available.</summary>
    public string? IpAddress { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
