namespace SchoolErp.Domain.Common;

/// <summary>
/// Base class for every business entity that belongs to a school (tenant).
/// Tenant isolation is enforced on this type in two layers:
/// an EF Core global query filter on <see cref="TenantId"/> and a
/// PostgreSQL row-level-security policy on the underlying table.
/// </summary>
public abstract class TenantEntity : AuditableEntity
{
    /// <summary>
    /// Owning tenant. Stamped automatically from the ambient tenant context on
    /// insert; never trust a client-supplied value.
    /// </summary>
    public Guid TenantId { get; set; }
}
