using SchoolErp.Application.Abstractions;

namespace SchoolErp.Api.Middleware;

/// <summary>
/// Resolves the tenant for every request before any business logic executes.
/// Resolution precedence (first match wins):
/// <list type="number">
/// <item>JWT claim <c>tenant</c> — authoritative for authenticated calls.</item>
/// <item><c>X-Tenant-Id</c> header — server-to-server integrations.</item>
/// <item>Exact custom-domain match on the Host header.</item>
/// <item>Subdomain of the platform base domain (school1.app.com → "school1").</item>
/// </list>
/// Requests to platform endpoints (health, swagger, super-admin catalog APIs)
/// run without a tenant. A resolved-but-inactive tenant is rejected with 403.
/// </summary>
public sealed partial class TenantResolutionMiddleware
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Rejected request for inactive tenant {TenantId}")]
    private static partial void LogInactiveTenant(ILogger logger, Guid tenantId);

    /// <summary>Claim type carrying the tenant id inside access tokens.</summary>
    public const string TenantClaim = "tenant";

    /// <summary>Header used by trusted server-to-server callers.</summary>
    public const string TenantHeader = "X-Tenant-Id";

    private readonly RequestDelegate _next;
    private readonly string _baseDomain;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _baseDomain = configuration["Tenancy:BaseDomain"] ?? "localhost";
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantLookup lookup,
        ITenantContextSetter tenantSetter)
    {
        var tenant = await ResolveAsync(context, lookup).ConfigureAwait(false);

        if (tenant is not null)
        {
            if (!tenant.IsActive)
            {
                LogInactiveTenant(_logger, tenant.Id);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://httpstatuses.io/403",
                    title = "School account is not active.",
                    status = StatusCodes.Status403Forbidden,
                }).ConfigureAwait(false);
                return;
            }

            tenantSetter.SetTenant(tenant.Id);
        }

        await _next(context).ConfigureAwait(false);
    }

    private async Task<TenantInfo?> ResolveAsync(HttpContext context, ITenantLookup lookup)
    {
        var ct = context.RequestAborted;

        // 1. JWT claim — set at login, cannot be forged past signature validation.
        var claimValue = context.User.FindFirst(TenantClaim)?.Value;
        if (Guid.TryParse(claimValue, out var claimTenantId))
        {
            return await lookup.FindByIdAsync(claimTenantId, ct).ConfigureAwait(false);
        }

        // 2. Explicit header (integrations, mobile pre-login calls).
        if (context.Request.Headers.TryGetValue(TenantHeader, out var headerValues) &&
            Guid.TryParse(headerValues.FirstOrDefault(), out var headerTenantId))
        {
            return await lookup.FindByIdAsync(headerTenantId, ct).ConfigureAwait(false);
        }

        // 3 & 4. Host-based resolution.
        var host = context.Request.Host.Host.ToLowerInvariant();

        if (!host.Equals(_baseDomain, StringComparison.OrdinalIgnoreCase) &&
            host.EndsWith("." + _baseDomain, StringComparison.OrdinalIgnoreCase))
        {
            var subdomain = host[..^(_baseDomain.Length + 1)];
            if (subdomain.Length > 0 && !subdomain.Contains('.'))
            {
                return await lookup.FindBySubdomainAsync(subdomain, ct).ConfigureAwait(false);
            }
        }

        return await lookup.FindByCustomDomainAsync(host, ct).ConfigureAwait(false);
    }
}
