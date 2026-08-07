using AutoMapper;
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
    public string? AffiliationBoard { get; init; }
    public string? LogoUrl { get; init; }
    public SubscriptionPlan Plan { get; init; }
    public DateOnly? SubscriptionExpiresOn { get; init; }
    public TenantModules EnabledModules { get; init; }
    public int StorageLimitMb { get; init; }
    public int SmsCredits { get; init; }
    public string TimeZoneId { get; init; } = string.Empty;
    public string DefaultLanguage { get; init; } = string.Empty;
    public TenantStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>AutoMapper profile for the tenant catalog module.</summary>
public sealed class TenantProfile : Profile
{
    public TenantProfile()
    {
        CreateMap<Tenant, TenantDto>();
    }
}
