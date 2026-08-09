using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Outbox;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Application.Billing;

/// <summary>What one school is actually consuming — the billing conversation.</summary>
public sealed record TenantUsageDto(
    string SchoolName,
    int ActiveStudents,
    int SmsCreditsRemaining,
    int SmsSentLast30Days,
    int PushSentLast30Days,
    decimal FeesCollectedLast30Days,
    decimal OutstandingInvoiceTotal);

/// <summary>Usage numbers for one school (platform view).</summary>
public sealed record GetTenantUsageQuery(Guid TenantId) : IRequest<TenantUsageDto>;

/// <summary>
/// Platform tables are read directly; RLS-protected tables (students, fee
/// payments) are read through a fresh scope pinned to the target tenant —
/// the same pattern the background jobs use.
/// </summary>
public sealed class GetTenantUsageQueryHandler : IRequestHandler<GetTenantUsageQuery, TenantUsageDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;

    public GetTenantUsageQueryHandler(
        IApplicationDbContext db, IServiceScopeFactory scopeFactory, TimeProvider clock)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _clock = clock;
    }

    public async Task<TenantUsageDto> Handle(
        GetTenantUsageQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.TenantId);

        var since = _clock.GetUtcNow().AddDays(-30);
        var sinceDate = DateOnly.FromDateTime(since.UtcDateTime);

        var smsSent = await _db.OutboxMessages.AsNoTracking()
            .CountAsync(m => m.TenantId == tenant.Id && m.Type == OutboxMessageTypes.Sms &&
                             m.ProcessedAt >= since, cancellationToken)
            .ConfigureAwait(false);
        var pushSent = await _db.OutboxMessages.AsNoTracking()
            .CountAsync(m => m.TenantId == tenant.Id && m.Type == OutboxMessageTypes.Push &&
                             m.ProcessedAt >= since, cancellationToken)
            .ConfigureAwait(false);

        var outstanding = await _db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == tenant.Id && i.Status == Domain.Billing.InvoiceStatus.Issued)
            .SumAsync(i => (decimal?)i.TotalAmount, cancellationToken)
            .ConfigureAwait(false) ?? 0;

        // Tenant-scoped reads through a pinned scope.
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(tenant.Id);
        var tenantDb = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var currentYearId = await tenantDb.AcademicYears.AsNoTracking()
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeStudents = currentYearId is { } yearId
            ? await tenantDb.Enrollments.AsNoTracking()
                .CountAsync(e => e.AcademicYearId == yearId &&
                                 e.Status == EnrollmentStatus.Active, cancellationToken)
                .ConfigureAwait(false)
            : 0;

        var feesCollected = await tenantDb.FeePayments.AsNoTracking()
            .Where(p => p.PaidOn >= sinceDate)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken)
            .ConfigureAwait(false) ?? 0;

        return new TenantUsageDto(
            tenant.Name,
            activeStudents,
            tenant.SmsCredits,
            smsSent,
            pushSent,
            feesCollected,
            outstanding);
    }
}
