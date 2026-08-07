namespace SchoolErp.Domain.Common;

/// <summary>
/// Base class for all persisted entities. Carries the platform-mandated
/// audit columns and optimistic-concurrency token.
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>Primary key. Client-generated GUID so aggregates can be wired before persistence.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>UTC instant the row was created. Stamped by the audit interceptor.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC instant of the last modification. Stamped by the audit interceptor.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>User id of the creator. Stamped by the audit interceptor.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>User id of the last modifier. Stamped by the audit interceptor.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Soft-delete flag. Hard deletes are converted to soft deletes by the audit
    /// interceptor; global query filters hide soft-deleted rows.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Optimistic-concurrency token, mapped to PostgreSQL's <c>xmin</c> system
    /// column (no extra storage, updated automatically on every write).
    /// </summary>
    public uint RowVersion { get; set; }
}
