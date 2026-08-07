using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure.Persistence;

namespace SchoolErp.Infrastructure.Tenancy;

/// <summary>
/// Catalog lookups for the tenant-resolution pipeline, memoized in-process.
/// Runs on every request, so results are cached briefly; a short TTL keeps
/// suspension/branding changes visible within a minute without a bus.
/// </summary>
public sealed class CachedTenantLookup : ITenantLookup
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;

    public CachedTenantLookup(IDbContextFactory<AppDbContext> contextFactory, IMemoryCache cache)
    {
        _contextFactory = contextFactory;
        _cache = cache;
    }

    public Task<TenantInfo?> FindByIdAsync(Guid tenantId, CancellationToken ct = default) =>
        LookupAsync($"tenant:id:{tenantId}", q => q.Where(t => t.Id == tenantId), ct);

    public Task<TenantInfo?> FindBySubdomainAsync(string subdomain, CancellationToken ct = default)
    {
        var normalized = subdomain.Trim().ToLowerInvariant();
        return LookupAsync($"tenant:sub:{normalized}", q => q.Where(t => t.Subdomain == normalized), ct);
    }

    public Task<TenantInfo?> FindByCustomDomainAsync(string host, CancellationToken ct = default)
    {
        var normalized = host.Trim().ToLowerInvariant();
        return LookupAsync($"tenant:dom:{normalized}", q => q.Where(t => t.CustomDomain == normalized), ct);
    }

    public Task<TenantInfo?> FindByCodeAsync(string schoolCode, CancellationToken ct = default)
    {
        var normalized = schoolCode.Trim().ToUpperInvariant();
        return LookupAsync($"tenant:code:{normalized}", q => q.Where(t => t.Code == normalized), ct);
    }

    private async Task<TenantInfo?> LookupAsync(
        string cacheKey,
        Func<IQueryable<Tenant>, IQueryable<Tenant>> filter,
        CancellationToken ct)
    {
        if (_cache.TryGetValue(cacheKey, out TenantInfo? cached))
        {
            return cached;
        }

        // A pooled factory context is used instead of the scoped AppDbContext:
        // resolution happens before any tenant is bound to the scope.
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var tenant = await filter(db.Tenants.AsNoTracking())
            .Select(t => new TenantInfo(
                t.Id,
                t.Code,
                t.Name,
                t.Subdomain,
                t.CustomDomain,
                t.Status == TenantStatus.Active,
                t.TimeZoneId,
                t.DefaultLanguage))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        // Negative results are cached too — unknown subdomains must not
        // hammer the catalog under scanner traffic.
        _cache.Set(cacheKey, tenant, CacheTtl);
        return tenant;
    }
}
