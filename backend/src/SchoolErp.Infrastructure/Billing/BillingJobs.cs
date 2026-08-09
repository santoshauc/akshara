using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Billing;
using SchoolErp.Domain.Billing;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure.Persistence;


namespace SchoolErp.Infrastructure.Billing;

/// <summary>
/// Nightly billing housekeeping:
///
/// 1. LICENCE RENEWALS — in the renewal month (default April, the Indian
///    academic-season start) every active paid-plan school without a licence
///    invoice for the coming year gets one, priced from the plan's
///    per-student rate × its current active enrollment.
/// 2. OVERDUE SUSPENSION — schools whose invoices are unpaid past the grace
///    period (default 30 days after due) are suspended, which blocks their
///    logins. Off by default; enable with Billing:AutoSuspend=true.
/// </summary>
public sealed partial class BillingCycleJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _clock;
    private readonly ILogger<BillingCycleJob> _logger;

    public BillingCycleJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        TimeProvider clock,
        ILogger<BillingCycleJob> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        await RenewLicencesAsync(today, ct).ConfigureAwait(false);
        await SuspendOverdueAsync(today, ct).ConfigureAwait(false);
    }

    private async Task RenewLicencesAsync(DateOnly today, CancellationToken ct)
    {
        var renewalMonth = _configuration.GetValue("Billing:RenewalMonth", 4);
        if (today.Month != renewalMonth)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var schools = await db.Tenants.AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active && t.Plan != SubscriptionPlan.Trial)
            .Select(t => new { t.Id, t.Plan })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var seasonTag = $"Annual licence {today.Year}-{(today.Year + 1) % 100:00}";
        var invoiced = 0;
        foreach (var school in schools)
        {
            var rate = PlanPresets.AnnualRatePerStudent(school.Plan);
            if (rate <= 0)
            {
                continue;
            }

            // Idempotent: one licence invoice per school per season.
            var already = await db.Invoices.AsNoTracking()
                .Where(i => i.TenantId == school.Id && i.Status != InvoiceStatus.Void)
                .AnyAsync(i => i.Lines.Any(l => EF.Functions.Like(l.Description, seasonTag + "%")), ct)
                .ConfigureAwait(false);
            if (already)
            {
                continue;
            }

            var students = await CountActiveStudentsAsync(school.Id, ct).ConfigureAwait(false);
            if (students == 0)
            {
                continue;
            }

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateInvoiceCommand(
                    school.Id,
                    today.AddDays(30),
                    [new InvoiceLineDto($"{seasonTag} · {school.Plan} plan", students, rate, 0)],
                    "Auto-generated season renewal."),
                ct).ConfigureAwait(false);
            invoiced++;
        }

        if (invoiced > 0)
        {
            LogRenewals(_logger, invoiced);
        }
    }

    private async Task<int> CountActiveStudentsAsync(Guid tenantId, CancellationToken ct)
    {
        // Enrollment counts sit behind RLS — pin a scope to the school.
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var yearId = await db.AcademicYears.AsNoTracking()
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (yearId is null)
        {
            return 0;
        }

        return await db.Enrollments.AsNoTracking()
            .CountAsync(e => e.AcademicYearId == yearId && e.Status == EnrollmentStatus.Active, ct)
            .ConfigureAwait(false);
    }

    private async Task SuspendOverdueAsync(DateOnly today, CancellationToken ct)
    {
        if (!_configuration.GetValue("Billing:AutoSuspend", false))
        {
            return;
        }

        var graceDays = _configuration.GetValue("Billing:SuspendGraceDays", 30);
        var cutoff = today.AddDays(-graceDays);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var overdueTenantIds = await db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued && i.DueOn < cutoff)
            .Select(i => i.TenantId)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var suspended = 0;
        foreach (var tenantId in overdueTenantIds)
        {
            var tenant = await db.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active, ct)
                .ConfigureAwait(false);
            if (tenant is null)
            {
                continue;
            }

            tenant.Status = TenantStatus.Suspended;
            suspended++;
            LogSuspension(_logger, tenant.Code, graceDays);
        }

        if (suspended > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Billing cycle: {Count} annual licence invoice(s) auto-generated")]
    private static partial void LogRenewals(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Billing cycle: school {Code} suspended — invoices unpaid beyond the {GraceDays}-day grace period")]
    private static partial void LogSuspension(ILogger logger, string code, int graceDays);
}
