namespace SchoolErp.Application.Abstractions;

/// <summary>
/// Read-only projection of a catalog tenant used by the resolution pipeline.
/// Deliberately small so it can be cached aggressively.
/// </summary>
public sealed record TenantInfo(
    Guid Id,
    string Code,
    string Name,
    string Subdomain,
    string? CustomDomain,
    bool IsActive,
    string TimeZoneId,
    string DefaultLanguage,
    Domain.TenantCatalog.TenantModules EnabledModules,
    DateOnly? SubscriptionExpiresOn)
{
    /// <summary>True when the subscription window has lapsed (date-only, IST-agnostic UTC date).</summary>
    public bool IsSubscriptionExpired(DateOnly today) =>
        SubscriptionExpiresOn is { } expires && expires < today;
}

/// <summary>
/// Resolves catalog tenants by the identifiers a request can carry.
/// Implementations must cache: this runs on every request.
/// </summary>
public interface ITenantLookup
{
    Task<TenantInfo?> FindByIdAsync(Guid tenantId, CancellationToken ct = default);

    Task<TenantInfo?> FindBySubdomainAsync(string subdomain, CancellationToken ct = default);

    Task<TenantInfo?> FindByCustomDomainAsync(string host, CancellationToken ct = default);

    /// <summary>Lookup by the short school code used in mobile-app login.</summary>
    Task<TenantInfo?> FindByCodeAsync(string schoolCode, CancellationToken ct = default);
}
