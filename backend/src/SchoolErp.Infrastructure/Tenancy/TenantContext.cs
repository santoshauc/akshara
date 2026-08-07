using SchoolErp.Application.Abstractions;

namespace SchoolErp.Infrastructure.Tenancy;

/// <summary>
/// Scoped holder for the ambient tenant. Registered once per request/job scope;
/// the resolution middleware writes it, everything else reads it.
/// </summary>
public sealed class TenantContext : ITenantContext, ITenantContextSetter
{
    private Guid? _tenantId;

    public Guid TenantId =>
        _tenantId ?? throw new InvalidOperationException(
            "Tenant has not been resolved for this scope. " +
            "Ensure tenant-resolution middleware runs before any tenant-scoped work.");

    public bool HasTenant => _tenantId.HasValue;

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        if (_tenantId.HasValue && _tenantId.Value != tenantId)
        {
            // Switching tenants inside one scope is always a bug (or an attack).
            throw new InvalidOperationException(
                $"Scope is already bound to tenant {_tenantId} and cannot be rebound to {tenantId}.");
        }

        _tenantId = tenantId;
    }
}
