using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.Billing;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Application.Platform;

/// <summary>How many institutions there are, and in what state.</summary>
public sealed record PlatformInstitutionsDto(
    int Total,
    int Active,
    int Provisioning,
    int Suspended,
    int Archived,
    int Schools,
    int Colleges,
    int OnboardedInWindow);

/// <summary>Everyone the platform serves, across every school.</summary>
public sealed record PlatformPeopleDto(
    int Students,
    int Teachers,
    int Guardians,
    int StaffAccounts,
    int Campuses);

/// <summary>
/// Money, from issued invoices only. There is no stored recurring price per
/// school, so MRR/ARR in the contractual sense cannot be computed — see
/// <see cref="AnnualisedLicenceValue"/> for the list-rate estimate that can.
/// </summary>
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

/// <summary>How loudly something needs looking at.</summary>
public enum AttentionSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2,
}

/// <summary>One thing a platform operator should deal with today.</summary>
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

/// <summary>Delivery pipeline health — the thing that silently breaks.</summary>
public sealed record PlatformHealthDto(
    int OutboxPending,
    int OutboxFailed,
    int? OldestPendingMinutes);

/// <summary>Institutions onboarded in one month.</summary>
public sealed record GrowthPointDto(int Year, int Month, int Institutions);

/// <summary>
/// The Super Admin command centre. Every field is measured; anything the
/// platform does not actually record is named in
/// <see cref="UnavailableMetrics"/> rather than estimated into a number that
/// looks authoritative.
/// </summary>
public sealed record PlatformDashboardDto(
    int WindowDays,
    DateTimeOffset GeneratedAt,
    PlatformInstitutionsDto Overview,
    PlatformPeopleDto People,
    PlatformRevenueDto Revenue,
    IReadOnlyList<PlanSliceDto> PlanMix,
    IReadOnlyList<PlatformInstitutionRowDto> Institutions,
    IReadOnlyList<AttentionItemDto> Attention,
    IReadOnlyList<PlatformActivityDto> Activity,
    PlatformHealthDto Health,
    IReadOnlyList<GrowthPointDto> Growth,
    IReadOnlyList<string> UnavailableMetrics);

/// <summary>Platform-wide figures over a trailing window (default 30 days).</summary>
public sealed record GetPlatformDashboardQuery(int WindowDays = 30)
    : IRequest<PlatformDashboardDto>;

/// <summary>
/// Assembles the dashboard. Reads the tenant catalog, invoices, the audit
/// trail and the outbox directly (none are RLS'd), and goes through
/// <see cref="IPlatformMetrics"/> for anything inside a school's own tables.
/// </summary>
public sealed class GetPlatformDashboardQueryHandler
    : IRequestHandler<GetPlatformDashboardQuery, PlatformDashboardDto>
{
    /// <summary>Below this a school will start losing parent notifications.</summary>
    private const int LowSmsCredits = 500;

    /// <summary>A renewal this close needs a conversation, not a reminder.</summary>
    private const int RenewalWarningDays = 30;

    /// <summary>Longer than this in Provisioning means onboarding stalled.</summary>
    private const int StalledProvisioningDays = 14;

    private readonly IApplicationDbContext _db;
    private readonly IPlatformMetrics _metrics;
    private readonly TimeProvider _clock;

    public GetPlatformDashboardQueryHandler(
        IApplicationDbContext db, IPlatformMetrics metrics, TimeProvider clock)
    {
        _db = db;
        _metrics = metrics;
        _clock = clock;
    }

    public async Task<PlatformDashboardDto> Handle(
        GetPlatformDashboardQuery request, CancellationToken cancellationToken)
    {
        var windowDays = Math.Clamp(request.WindowDays, 1, 730);
        var now = _clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var windowStart = now.AddDays(-windowDays);
        var windowStartDate = DateOnly.FromDateTime(windowStart.UtcDateTime);

        var tenants = await _db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var counts = (await _metrics.GetTenantCountsAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(c => c.TenantId);

        var invoices = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Void)
            .Select(i => new
            {
                i.TenantId, i.Status, i.IssuedOn, i.DueOn, i.PaidOn, i.TotalAmount,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var staffAccounts = await _metrics.CountSchoolUsersAsync(cancellationToken)
            .ConfigureAwait(false);

        // --- institutions ----------------------------------------------------
        var institutions = new PlatformInstitutionsDto(
            Total: tenants.Count,
            Active: tenants.Count(t => t.Status == TenantStatus.Active),
            Provisioning: tenants.Count(t => t.Status == TenantStatus.Provisioning),
            Suspended: tenants.Count(t => t.Status == TenantStatus.Suspended),
            Archived: tenants.Count(t => t.Status == TenantStatus.Archived),
            Schools: tenants.Count(t => t.InstitutionType == InstitutionType.School),
            Colleges: tenants.Count(t => t.InstitutionType == InstitutionType.College),
            OnboardedInWindow: tenants.Count(t => t.CreatedAt >= windowStart));

        // --- people ----------------------------------------------------------
        var people = new PlatformPeopleDto(
            Students: counts.Values.Sum(c => c.ActiveStudents),
            Teachers: counts.Values.Sum(c => c.ActiveTeachers),
            Guardians: counts.Values.Sum(c => c.Guardians),
            StaffAccounts: staffAccounts,
            Campuses: counts.Values.Sum(c => c.OpenCampuses));

        // --- money -----------------------------------------------------------
        var outstandingByTenant = invoices
            .Where(i => i.Status == InvoiceStatus.Issued)
            .GroupBy(i => i.TenantId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.TotalAmount));

        var overdue = invoices
            .Where(i => i.Status == InvoiceStatus.Issued && i.DueOn < today)
            .ToList();

        // List rate × enrolled students of live schools. Labelled as an
        // estimate everywhere it surfaces: it is what the price list implies,
        // not what anyone has agreed to pay.
        var annualisedLicenceValue = tenants
            .Where(t => t.Status == TenantStatus.Active)
            .Sum(t => PlanPresets.AnnualRatePerStudent(t.Plan) * StudentsOf(counts, t.Id));

        var revenue = new PlatformRevenueDto(
            BilledInWindow: invoices.Where(i => i.IssuedOn >= windowStartDate).Sum(i => i.TotalAmount),
            CollectedInWindow: invoices
                .Where(i => i.Status == InvoiceStatus.Paid && i.PaidOn >= windowStartDate)
                .Sum(i => i.TotalAmount),
            Outstanding: outstandingByTenant.Values.Sum(),
            Overdue: overdue.Sum(i => i.TotalAmount),
            OverdueInvoices: overdue.Count,
            AnnualisedLicenceValue: annualisedLicenceValue);

        // --- plan mix ---------------------------------------------------------
        var planMix = tenants
            .Where(t => t.Status is TenantStatus.Active or TenantStatus.Provisioning)
            .GroupBy(t => t.Plan)
            .Select(g => new PlanSliceDto(
                g.Key, g.Count(), g.Sum(t => StudentsOf(counts, t.Id))))
            .OrderBy(s => s.Plan)
            .ToList();

        // --- the table --------------------------------------------------------
        var rows = tenants
            .Select(t => new PlatformInstitutionRowDto(
                t.Id, t.Code, t.Name, t.InstitutionType, t.Plan, t.Status,
                StudentsOf(counts, t.Id),
                counts.TryGetValue(t.Id, out var c) ? c.ActiveTeachers : 0,
                counts.TryGetValue(t.Id, out var cc) ? cc.OpenCampuses : 0,
                outstandingByTenant.TryGetValue(t.Id, out var due) ? due : 0m,
                t.SubscriptionExpiresOn,
                t.SmsCredits,
                t.CreatedAt))
            .ToList();

        // --- what needs doing --------------------------------------------------
        var attention = BuildAttention(tenants, rows, overdue.Count, today, now);

        // --- recent activity ---------------------------------------------------
        var names = tenants.ToDictionary(t => t.Id, t => t.Name);
        var activity = (await _db.AuditEvents
                .AsNoTracking()
                .OrderByDescending(a => a.OccurredAt)
                .Take(15)
                .Select(a => new { a.OccurredAt, a.Action, a.Detail, a.UserName, a.TenantId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(a => new PlatformActivityDto(
                a.OccurredAt, a.Action, a.Detail, a.UserName,
                a.TenantId is { } id && names.TryGetValue(id, out var name) ? name : null))
            .ToList();

        // --- delivery health -----------------------------------------------------
        var pending = await _db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.ProcessedAt == null)
            .Select(m => new { m.Attempts, m.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var health = new PlatformHealthDto(
            OutboxPending: pending.Count(m => m.Attempts < 5),
            OutboxFailed: pending.Count(m => m.Attempts >= 5),
            OldestPendingMinutes: pending.Count == 0
                ? null
                : (int)(now - pending.Min(m => m.CreatedAt)).TotalMinutes);

        // --- growth ----------------------------------------------------------
        var growth = BuildGrowth(tenants, now);

        return new PlatformDashboardDto(
            windowDays,
            now,
            institutions,
            people,
            revenue,
            planMix,
            rows,
            attention,
            activity,
            health,
            growth,
            UnavailableMetrics: NotMeasured);
    }

    /// <summary>
    /// Named so the UI can say "not measured yet" instead of drawing an empty
    /// chart that reads as zero. Each of these needs instrumentation that does
    /// not exist; none of them is going to be guessed at here.
    /// </summary>
    private static readonly string[] NotMeasured =
    [
        "Per-module feature adoption — no usage events are recorded.",
        "Daily and monthly active users — sign-ins are not aggregated into sessions.",
        "Mobile app adoption — installs are not reported back to the platform.",
        "Email deliverability — the outbox carries SMS and push only.",
        "Storage consumed per school — uploads are not metered against the quota.",
        "Contractual MRR/ARR — no recurring price is stored per school; the annualised figure is the list rate applied to enrolled students.",
    ];

    private static int StudentsOf(IReadOnlyDictionary<Guid, TenantCounts> counts, Guid tenantId) =>
        counts.TryGetValue(tenantId, out var c) ? c.ActiveStudents : 0;

    private static List<AttentionItemDto> BuildAttention(
        List<Tenant> tenants,
        List<PlatformInstitutionRowDto> rows,
        int overdueInvoices,
        DateOnly today,
        DateTimeOffset now)
    {
        var items = new List<AttentionItemDto>();
        var byId = rows.ToDictionary(r => r.Id);

        foreach (var tenant in tenants)
        {
            var row = byId[tenant.Id];

            if (tenant.SubscriptionExpiresOn is { } expiry)
            {
                if (expiry < today && tenant.Status == TenantStatus.Active)
                {
                    items.Add(new AttentionItemDto(
                        AttentionSeverity.Critical,
                        "Subscription expired",
                        $"Expired {today.DayNumber - expiry.DayNumber} day(s) ago; logins are blocked.",
                        tenant.Id, tenant.Name));
                }
                else if (expiry >= today && expiry.DayNumber - today.DayNumber <= RenewalWarningDays)
                {
                    items.Add(new AttentionItemDto(
                        AttentionSeverity.Warning,
                        "Renewal due",
                        $"Subscription ends {expiry:dd MMM yyyy}.",
                        tenant.Id, tenant.Name));
                }
            }

            if (row.Outstanding > 0 && tenant.Status != TenantStatus.Archived)
            {
                items.Add(new AttentionItemDto(
                    AttentionSeverity.Warning,
                    "Unpaid invoices",
                    $"₹{row.Outstanding:N0} outstanding.",
                    tenant.Id, tenant.Name));
            }

            if (tenant.Status == TenantStatus.Suspended)
            {
                items.Add(new AttentionItemDto(
                    AttentionSeverity.Critical,
                    "School suspended",
                    "Nobody at this school can sign in.",
                    tenant.Id, tenant.Name));
            }

            if (tenant.Status == TenantStatus.Active && tenant.SmsCredits < LowSmsCredits)
            {
                items.Add(new AttentionItemDto(
                    AttentionSeverity.Warning,
                    "SMS credits low",
                    $"{tenant.SmsCredits:N0} left — parent notifications stop at zero.",
                    tenant.Id, tenant.Name));
            }

            // Live, but nobody has been admitted: onboarding was never finished.
            if (tenant.Status == TenantStatus.Active && row.Students == 0)
            {
                items.Add(new AttentionItemDto(
                    AttentionSeverity.Info,
                    "No students yet",
                    "The school is active but has no enrolled students.",
                    tenant.Id, tenant.Name));
            }

            if (tenant.Status == TenantStatus.Provisioning &&
                (now - tenant.CreatedAt).TotalDays > StalledProvisioningDays)
            {
                items.Add(new AttentionItemDto(
                    AttentionSeverity.Warning,
                    "Onboarding stalled",
                    $"Still provisioning after {(int)(now - tenant.CreatedAt).TotalDays} days.",
                    tenant.Id, tenant.Name));
            }
        }

        if (overdueInvoices > 0)
        {
            items.Insert(0, new AttentionItemDto(
                AttentionSeverity.Critical,
                "Invoices past due",
                $"{overdueInvoices} invoice(s) are past their due date.",
                null, null));
        }

        return items
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.TenantName)
            .ToList();
    }

    /// <summary>
    /// Onboardings per month for the last twelve months, months with none
    /// included — a gap in a growth chart should read as zero, not as absence.
    /// </summary>
    private static List<GrowthPointDto> BuildGrowth(List<Tenant> tenants, DateTimeOffset now)
    {
        var points = new List<GrowthPointDto>(12);
        for (var offset = 11; offset >= 0; offset--)
        {
            var month = now.AddMonths(-offset);
            points.Add(new GrowthPointDto(
                month.Year,
                month.Month,
                tenants.Count(t =>
                    t.CreatedAt.Year == month.Year && t.CreatedAt.Month == month.Month)));
        }

        return points;
    }
}
