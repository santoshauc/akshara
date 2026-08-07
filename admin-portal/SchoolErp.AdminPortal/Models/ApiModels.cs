using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.AdminPortal.Models;

/// <summary>Token pair returned by the auth endpoints.</summary>
public sealed record AuthTokensDto(string AccessToken, int ExpiresInSeconds, string RefreshToken);

/// <summary>Password login payload (SchoolCode empty = platform login).</summary>
public sealed record LoginRequest(string SchoolCode, string Login, string Password);

/// <summary>School as returned by the tenant APIs.</summary>
public sealed record TenantDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Subdomain { get; init; } = string.Empty;
    public string? CustomDomain { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public string? AffiliationBoard { get; init; }
    public string? LogoUrl { get; init; }
    public SubscriptionPlan Plan { get; init; }
    public DateOnly? SubscriptionExpiresOn { get; init; }
    public TenantModules EnabledModules { get; init; }
    public int StorageLimitMb { get; init; }
    public int SmsCredits { get; init; }
    public string TimeZoneId { get; init; } = "Asia/Kolkata";
    public string DefaultLanguage { get; init; } = "en";
    public TenantStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Onboarding payload (mirrors CreateTenantCommand).</summary>
public sealed record CreateTenantRequest(
    string Code,
    string Name,
    string Subdomain,
    string? CustomDomain,
    string? ContactEmail,
    string? ContactPhone,
    string? City,
    string? State,
    string? AffiliationBoard,
    SubscriptionPlan Plan,
    TenantModules EnabledModules,
    string TimeZoneId,
    string DefaultLanguage);

/// <summary>Update payload (mirrors UpdateTenantCommand).</summary>
public sealed record UpdateTenantRequest(
    Guid Id,
    string Name,
    string? CustomDomain,
    string? ContactEmail,
    string? ContactPhone,
    string? City,
    string? State,
    string? AffiliationBoard,
    string? LogoUrl,
    string? ThemePrimaryColor,
    string? ThemeSecondaryColor,
    SubscriptionPlan Plan,
    DateOnly? SubscriptionExpiresOn,
    TenantModules EnabledModules,
    int StorageLimitMb,
    int SmsCredits,
    string TimeZoneId,
    string DefaultLanguage);
