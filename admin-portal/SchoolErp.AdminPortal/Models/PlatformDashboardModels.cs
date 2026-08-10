using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.AdminPortal.Models;

/// <summary>Institution counts by state and kind.</summary>
public sealed record PlatformInstitutionsDto(
    int Total,
    int Active,
    int Provisioning,
    int Suspended,
    int Archived,
    int Schools,
    int Colleges,
    int OnboardedInWindow);

/// <summary>Everyone the platform serves.</summary>
public sealed record PlatformPeopleDto(
    int Students,
    int Teachers,
    int Guardians,
    int StaffAccounts,
    int Campuses);

/// <summary>Invoiced money, plus the list-rate annualised estimate.</summary>
public sealed record PlatformRevenueDto(
    decimal BilledInWindow,
    decimal CollectedInWindow,
    decimal Outstanding,
    decimal Overdue,
    int OverdueInvoices,
    decimal AnnualisedLicenceValue);

/// <summary>One plan and what sits on it.</summary>
public sealed record PlanSliceDto(SubscriptionPlan Plan, int Institutions, int Students);

/// <summary>A school as the platform table lists it.</summary>
public sealed record PlatformInstitutionRowDto(
    Guid Id,
    string Code,
    string Name,
    InstitutionType InstitutionType,
    SubscriptionPlan Plan,
    TenantStatus Status,
    int Students,
    int Teachers,
    int Campuses,
    decimal Outstanding,
    DateOnly? SubscriptionExpiresOn,
    int SmsCredits,
    DateTimeOffset CreatedAt);

/// <summary>How loudly something needs looking at (mirrors the API enum).</summary>
public enum AttentionSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2,
}

/// <summary>One thing an operator should deal with today.</summary>
public sealed record AttentionItemDto(
    AttentionSeverity Severity,
    string Title,
    string Detail,
    Guid? TenantId,
    string? TenantName);

/// <summary>A recent action anywhere on the platform.</summary>
public sealed record PlatformActivityDto(
    DateTimeOffset OccurredAt,
    string Action,
    string? Detail,
    string? UserName,
    string? TenantName);

/// <summary>Delivery pipeline health.</summary>
public sealed record PlatformHealthDto(
    int OutboxPending,
    int OutboxFailed,
    int? OldestPendingMinutes);

/// <summary>Institutions onboarded in one month.</summary>
public sealed record GrowthPointDto(int Year, int Month, int Institutions);

/// <summary>Everything the Super Admin dashboard renders.</summary>
public sealed record PlatformDashboardDto(
    int WindowDays,
    DateTimeOffset GeneratedAt,
    PlatformInstitutionsDto Overview,
    PlatformPeopleDto People,
    PlatformRevenueDto Revenue,
    List<PlanSliceDto> PlanMix,
    List<PlatformInstitutionRowDto> Institutions,
    List<AttentionItemDto> Attention,
    List<PlatformActivityDto> Activity,
    PlatformHealthDto Health,
    List<GrowthPointDto> Growth,
    List<string> UnavailableMetrics);
