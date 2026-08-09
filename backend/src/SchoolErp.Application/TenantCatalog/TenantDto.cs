using System.Linq.Expressions;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Application.TenantCatalog;

/// <summary>Catalog projection of a school returned by the tenant APIs.</summary>
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

    /// <summary>School or college — they count different things.</summary>
    public InstitutionType InstitutionType { get; init; }

    /// <summary>Every board this school is affiliated to, with its own number.</summary>
    public IReadOnlyList<TenantAffiliationDto> Affiliations { get; init; } = [];
    public string? LogoUrl { get; init; }
    public string? ThemePrimaryColor { get; init; }
    public string? ThemeSecondaryColor { get; init; }
    public SubscriptionPlan Plan { get; init; }
    public DateOnly? SubscriptionExpiresOn { get; init; }
    public TenantModules EnabledModules { get; init; }
    public int StorageLimitMb { get; init; }
    public int SmsCredits { get; init; }

    /// <summary>Prefer WhatsApp for parent notifications (SMS fallback).</summary>
    public bool WhatsAppEnabled { get; init; }
    public string TimeZoneId { get; init; } = string.Empty;
    public string DefaultLanguage { get; init; } = string.Empty;
    public TenantStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>One board affiliation and its number.</summary>
public sealed record TenantAffiliationDto(string Board, string? AffiliationNumber);

/// <summary>Hand-written projection (EF-translatable + in-memory).</summary>
public static class TenantMappings
{
    /// <summary>EF-translatable projection for query composition.</summary>
    public static readonly Expression<Func<Tenant, TenantDto>> Projection =
        tenant => new TenantDto
        {
            Id = tenant.Id,
            Code = tenant.Code,
            Name = tenant.Name,
            Subdomain = tenant.Subdomain,
            CustomDomain = tenant.CustomDomain,
            City = tenant.City,
            State = tenant.State,
            ContactEmail = tenant.ContactEmail,
            ContactPhone = tenant.ContactPhone,
            InstitutionType = tenant.InstitutionType,
            Affiliations = tenant.Affiliations
                .Select(a => new TenantAffiliationDto(a.Board, a.AffiliationNumber))
                .ToList(),
            LogoUrl = tenant.LogoUrl,
            ThemePrimaryColor = tenant.ThemePrimaryColor,
            ThemeSecondaryColor = tenant.ThemeSecondaryColor,
            Plan = tenant.Plan,
            SubscriptionExpiresOn = tenant.SubscriptionExpiresOn,
            EnabledModules = tenant.EnabledModules,
            StorageLimitMb = tenant.StorageLimitMb,
            SmsCredits = tenant.SmsCredits,
            WhatsAppEnabled = tenant.WhatsAppEnabled,
            TimeZoneId = tenant.TimeZoneId,
            DefaultLanguage = tenant.DefaultLanguage,
            Status = tenant.Status,
            CreatedAt = tenant.CreatedAt,
        };

    private static readonly Func<Tenant, TenantDto> Compiled = Projection.Compile();

    public static TenantDto ToDto(this Tenant tenant) => Compiled(tenant);
}
