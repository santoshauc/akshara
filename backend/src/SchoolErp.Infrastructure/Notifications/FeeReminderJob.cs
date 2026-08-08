using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Attendance;
using SchoolErp.Domain.Outbox;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure.Persistence;

namespace SchoolErp.Infrastructure.Notifications;

/// <summary>
/// Nightly fee-due reminders: for every active school, finds students whose
/// overdue balance (plan lines past due minus payments and concessions) is
/// positive and queues one SMS to the primary guardian via the outbox — so
/// SMS-credit metering applies. A guardian is reminded for a given child at
/// most once every 6 days.
/// </summary>
public sealed partial class FeeReminderJob
{
    private const string MessagePrefix = "Fee reminder:";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<FeeReminderJob> _logger;

    public FeeReminderJob(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<FeeReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        List<Guid> tenantIds;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            tenantIds = await db.Tenants.AsNoTracking()
                .Where(t => t.Status == TenantStatus.Active)
                .Select(t => t.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        var queuedTotal = 0;
        foreach (var tenantId in tenantIds)
        {
            queuedTotal += await RemindTenantAsync(tenantId, ct).ConfigureAwait(false);
        }

        if (queuedTotal > 0)
        {
            LogQueued(_logger, queuedTotal);
        }
    }

    private async Task<int> RemindTenantAsync(Guid tenantId, CancellationToken ct)
    {
        // Fresh scope per tenant: RLS and EF filters need the bound tenant.
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        var currentYearId = await db.AcademicYears.AsNoTracking()
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (currentYearId is not { } yearId)
        {
            return 0;
        }

        var overdueByClass = await db.FeeStructureItems.AsNoTracking()
            .Where(i => i.AcademicYearId == yearId && i.DueDate < today)
            .GroupBy(i => i.SchoolClassId)
            .Select(g => new { ClassId = g.Key, Overdue = g.Sum(i => i.Amount) })
            .ToDictionaryAsync(g => g.ClassId, g => g.Overdue, ct)
            .ConfigureAwait(false);
        if (overdueByClass.Count == 0)
        {
            return 0;
        }

        var students = await db.Enrollments.AsNoTracking()
            .Where(e => e.AcademicYearId == yearId && e.Status == EnrollmentStatus.Active)
            .Join(db.Students, e => e.StudentId, s => s.Id, (e, s) => new
            {
                e.StudentId,
                e.SchoolClassId,
                StudentName = (s.FirstName + " " + s.LastName).Trim(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var paidByStudent = await db.FeePayments.AsNoTracking()
            .Where(p => p.AcademicYearId == yearId)
            .GroupBy(p => p.StudentId)
            .Select(g => new { g.Key, Paid = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(g => g.Key, g => g.Paid, ct)
            .ConfigureAwait(false);

        var concessionByStudent = await db.FeeConcessions.AsNoTracking()
            .Where(c => c.AcademicYearId == yearId)
            .GroupBy(c => c.StudentId)
            .Select(g => new { g.Key, Amount = g.Sum(c => c.Amount) })
            .ToDictionaryAsync(g => g.Key, g => g.Amount, ct)
            .ConfigureAwait(false);

        var guardianByStudent = await db.StudentGuardians.AsNoTracking()
            .Where(g => g.IsPrimary)
            .Select(g => new { g.StudentId, g.Guardian!.Phone })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var phoneByStudent = guardianByStudent
            .GroupBy(g => g.StudentId)
            .ToDictionary(g => g.Key, g => g.First().Phone);

        // At most one reminder per guardian+child per 6 days: recent queued
        // reminders are recognized by the message prefix in the payload.
        // Payload is jsonb, so substring matching happens client-side over
        // the (small) window of recent SMS rows.
        var since = _clock.GetUtcNow().AddDays(-6);
        var recentPayloads = (await db.OutboxMessages.AsNoTracking()
                .Where(m => m.TenantId == tenantId && m.CreatedAt > since &&
                            m.Type == OutboxMessageTypes.Sms)
                .Select(m => m.Payload)
                .ToListAsync(ct)
                .ConfigureAwait(false))
            .Where(p => p.Contains(MessagePrefix))
            .ToList();

        var schoolName = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstAsync(ct)
            .ConfigureAwait(false);

        var queued = 0;
        foreach (var student in students)
        {
            if (!overdueByClass.TryGetValue(student.SchoolClassId, out var overdue))
            {
                continue;
            }

            var balance = overdue
                - paidByStudent.GetValueOrDefault(student.StudentId)
                - concessionByStudent.GetValueOrDefault(student.StudentId);
            if (balance <= 0 ||
                !phoneByStudent.TryGetValue(student.StudentId, out var phone) ||
                recentPayloads.Any(p => p.Contains(phone) && p.Contains(student.StudentName)))
            {
                continue;
            }

            db.OutboxMessages.Add(new OutboxMessage
            {
                TenantId = tenantId,
                Type = OutboxMessageTypes.Sms,
                Payload = JsonSerializer.Serialize(new SmsPayload(
                    phone,
                    $"{MessagePrefix} ₹{balance:N0} is overdue for {student.StudentName} " +
                    $"at {schoolName}. Please pay at the school office or in the parent app.")),
            });
            queued++;
        }

        if (queued > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return queued;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Fee reminders queued: {Count} SMS across all schools")]
    private static partial void LogQueued(ILogger logger, int count);
}
