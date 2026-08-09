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
    public InstitutionType InstitutionType { get; init; } = InstitutionType.School;
    /// <summary>Every board this school is affiliated to.</summary>
    public List<TenantAffiliationDto> Affiliations { get; init; } = [];
    public string? LogoUrl { get; init; }
    public string? ThemePrimaryColor { get; init; }
    public string? ThemeSecondaryColor { get; init; }
    public SubscriptionPlan Plan { get; init; }
    public DateOnly? SubscriptionExpiresOn { get; init; }
    public TenantModules EnabledModules { get; init; }
    public int StorageLimitMb { get; init; }
    public int SmsCredits { get; init; }
    public bool WhatsAppEnabled { get; init; }
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
    List<TenantAffiliationDto>? Affiliations,
    SubscriptionPlan Plan,
    TenantModules EnabledModules,
    string TimeZoneId,
    string DefaultLanguage,
    InstitutionType InstitutionType);

/// <summary>One board affiliation (mirrors TenantAffiliationDto).</summary>
public sealed record TenantAffiliationDto(string Board, string? AffiliationNumber);

/// <summary>Update payload (mirrors UpdateTenantCommand).</summary>
public sealed record UpdateTenantRequest(
    Guid Id,
    string Name,
    string? CustomDomain,
    string? ContactEmail,
    string? ContactPhone,
    string? City,
    string? State,
    List<TenantAffiliationDto>? Affiliations,
    string? LogoUrl,
    string? ThemePrimaryColor,
    string? ThemeSecondaryColor,
    SubscriptionPlan Plan,
    DateOnly? SubscriptionExpiresOn,
    TenantModules EnabledModules,
    int StorageLimitMb,
    int SmsCredits,
    bool WhatsAppEnabled,
    string TimeZoneId,
    string DefaultLanguage,
    InstitutionType InstitutionType);

/// <summary>Public branding served anonymously by school code.</summary>
public sealed record TenantBrandingDto(
    string Name,
    string? LogoUrl,
    string? ThemePrimaryColor,
    string? ThemeSecondaryColor);

/// <summary>Invoice lifecycle (mirrors the API enum).</summary>
public enum InvoiceStatus
{
    Issued = 1,
    Paid = 2,
    Void = 3,
}

/// <summary>One invoice line.</summary>
public sealed record InvoiceLineDto(string Description, decimal Quantity, decimal UnitAmount, decimal Amount);

/// <summary>A platform invoice to a school.</summary>
public sealed record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid TenantId,
    string SchoolName,
    InvoiceStatus Status,
    DateOnly IssuedOn,
    DateOnly DueOn,
    DateOnly? PaidOn,
    decimal TotalAmount,
    string? Notes,
    List<InvoiceLineDto> Lines);

/// <summary>What one school is consuming.</summary>
public sealed record TenantUsageDto(
    string SchoolName,
    int ActiveStudents,
    int SmsCreditsRemaining,
    int SmsSentLast30Days,
    int PushSentLast30Days,
    decimal FeesCollectedLast30Days,
    decimal OutstandingInvoiceTotal);

/// <summary>The school's own subscription view.</summary>
public sealed record MySubscriptionDto(
    SubscriptionPlan Plan,
    DateOnly? ExpiresOn,
    List<string> EnabledModules,
    int SmsCredits,
    decimal OutstandingTotal,
    List<InvoiceDto> Invoices);
