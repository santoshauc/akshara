namespace SchoolErp.Application.Abstractions;

/// <summary>
/// Ambient tenant for the current request/job scope. Everything downstream of
/// tenant resolution (query filters, audit stamping, RLS session variable)
/// reads from this — business code must never carry tenant ids manually.
/// </summary>
public interface ITenantContext
{
    /// <summary>Resolved tenant id. Throws if accessed before resolution — see <see cref="HasTenant"/>.</summary>
    Guid TenantId { get; }

    /// <summary>True once a tenant has been resolved for this scope.</summary>
    bool HasTenant { get; }
}

/// <summary>
/// Write side of the tenant context. Only the tenant-resolution middleware and
/// background-job infrastructure may set the tenant; keeping the setter on a
/// separate interface stops business code from switching tenants mid-request.
/// </summary>
public interface ITenantContextSetter
{
    /// <summary>Binds the scope to a tenant. May be called once per scope.</summary>
    void SetTenant(Guid tenantId);
}
