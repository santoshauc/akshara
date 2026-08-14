using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Outbox;

/// <summary>Well-known outbox message types.</summary>
public static class OutboxMessageTypes
{
    public const string Sms = "sms";

    public const string Push = "push";

    public const string Email = "email";
}

/// <summary>
/// Transactional-outbox row: side effects (SMS, push, email) are written in
/// the same transaction as the business change and delivered asynchronously
/// by the dispatcher. Deliberately NOT a <see cref="TenantEntity"/> — the
/// dispatcher runs without a tenant scope, so this table has no RLS; the
/// explicit <see cref="TenantId"/> exists for audit and per-tenant metering.
/// </summary>
public class OutboxMessage : AuditableEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Message kind — one of <see cref="OutboxMessageTypes"/>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>JSON payload, shape defined per <see cref="Type"/>.</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Delivery attempts so far; the dispatcher gives up after 5.</summary>
    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
