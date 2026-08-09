using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.Admissions;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Leave;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Dashboard;

/// <summary>An upcoming exam on the dashboard.</summary>
public sealed record UpcomingExamDto(string Name, DateOnly StartDate);

/// <summary>One point on a small dashboard trend.</summary>
public sealed record DashboardPointDto(DateOnly Date, decimal Value);

/// <summary>A student celebrating today — schools announce these at assembly.</summary>
public sealed record BirthdayDto(string Name, string? ClassName, int TurnsAge);

/// <summary>The school's at-a-glance numbers, all scoped to the tenant.</summary>
public sealed record DashboardDto(
    int ActiveStudents,
    int AttendanceMarkedToday,
    int PresentToday,
    decimal AttendanceTodayPercent,
    decimal FeesCollectedThisMonth,
    int OverdueLoans,
    int PendingLeaveRequests,
    int UnreadParentMessages,
    int OpenEnquiries,
    int EnquiryFollowUpsDueToday,
    IReadOnlyList<UpcomingExamDto> UpcomingExams,
    IReadOnlyList<DashboardPointDto> AttendanceTrend,
    IReadOnlyList<DashboardPointDto> FeeTrend,
    IReadOnlyList<BirthdayDto> BirthdaysToday,
    decimal FeesOutstanding,
    int SmsCredits,
    DateOnly? SubscriptionExpiresOn);

/// <summary>The staff dashboard tiles.</summary>
public sealed record GetDashboardQuery : IRequest<DashboardDto>;

/// <summary>Aggregates the hot numbers; every query is tenant-filtered.</summary>
public sealed class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public GetDashboardQueryHandler(
        IApplicationDbContext db, ITenantContext tenantContext, TimeProvider clock)
    {
        _db = db;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<DashboardDto> Handle(
        GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var currentYearId = await _db.AcademicYears.AsNoTracking()
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeStudents = currentYearId is { } yearId
            ? await _db.Enrollments.AsNoTracking()
                .CountAsync(e => e.AcademicYearId == yearId &&
                                 e.Status == EnrollmentStatus.Active, cancellationToken)
                .ConfigureAwait(false)
            : 0;

        var todayMarks = await _db.AttendanceRecords.AsNoTracking()
            .Where(a => a.Date == today && a.Period == null)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Marked = g.Count(),
                Present = g.Count(a => a.Status == AttendanceStatus.Present ||
                                       a.Status == AttendanceStatus.Late ||
                                       a.Status == AttendanceStatus.HalfDay),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var feesThisMonth = await _db.FeePayments.AsNoTracking()
            .Where(p => p.PaidOn >= monthStart && p.PaidOn <= today)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken)
            .ConfigureAwait(false) ?? 0;

        var overdueLoans = await _db.BookLoans.AsNoTracking()
            .CountAsync(l => l.ReturnedOn == null && l.DueOn < today, cancellationToken)
            .ConfigureAwait(false);

        var pendingLeave = await _db.LeaveRequests.AsNoTracking()
            .CountAsync(l => l.Status == LeaveRequestStatus.Pending, cancellationToken)
            .ConfigureAwait(false);

        var unreadMessages = await _db.StudentMessages.AsNoTracking()
            .CountAsync(m => !m.SentByStaff && m.ReadByStaffAt == null, cancellationToken)
            .ConfigureAwait(false);

        var openEnquiries = await _db.AdmissionEnquiries.AsNoTracking()
            .CountAsync(e => e.Status != EnquiryStatus.Admitted &&
                             e.Status != EnquiryStatus.Lost, cancellationToken)
            .ConfigureAwait(false);

        var followUpsDue = await _db.AdmissionEnquiries.AsNoTracking()
            .CountAsync(e => e.FollowUpOn != null && e.FollowUpOn <= today &&
                             e.Status != EnquiryStatus.Admitted &&
                             e.Status != EnquiryStatus.Lost, cancellationToken)
            .ConfigureAwait(false);

        var upcomingExams = currentYearId is { } examYearId
            ? await _db.Exams.AsNoTracking()
                .Where(e => e.AcademicYearId == examYearId && e.StartDate >= today)
                .OrderBy(e => e.StartDate)
                .Take(3)
                .Select(e => new UpcomingExamDto(e.Name, e.StartDate))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : [];

        // 14-day attendance % trend (daily roll-call only).
        var trendFrom = today.AddDays(-13);
        var attendanceTrend = (await _db.AttendanceRecords.AsNoTracking()
                .Where(a => a.Period == null && a.Date >= trendFrom && a.Date <= today)
                .GroupBy(a => a.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Marked = g.Count(),
                    Present = g.Count(a => a.Status == AttendanceStatus.Present ||
                                           a.Status == AttendanceStatus.Late ||
                                           a.Status == AttendanceStatus.HalfDay),
                })
                .OrderBy(g => g.Date)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(g => new DashboardPointDto(g.Date, Math.Round(g.Present * 100m / g.Marked, 1)))
            .ToList();

        // 14-day daily collections (quiet days included as zero for even bars).
        var feeRaw = await _db.FeePayments.AsNoTracking()
            .Where(p => p.PaidOn >= trendFrom && p.PaidOn <= today)
            .GroupBy(p => p.PaidOn)
            .Select(g => new { Date = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var feeTrend = new List<DashboardPointDto>(14);
        for (var day = trendFrom; day <= today; day = day.AddDays(1))
        {
            feeTrend.Add(new DashboardPointDto(
                day, feeRaw.FirstOrDefault(f => f.Date == day)?.Amount ?? 0m));
        }

        // Today's birthdays — assembly announcements write themselves.
        List<BirthdayDto> birthdays = [];
        if (currentYearId is { } birthdayYearId)
        {
            var birthdayRows = await _db.Students.AsNoTracking()
                .Where(s => s.Status == StudentStatus.Active &&
                            s.DateOfBirth.Month == today.Month &&
                            s.DateOfBirth.Day == today.Day)
                .Select(s => new
                {
                    s.FirstName,
                    s.LastName,
                    s.DateOfBirth,
                    ClassName = s.Enrollments
                        .Where(e => e.AcademicYearId == birthdayYearId)
                        .Select(e => e.SchoolClass!.Name + " " + e.Section!.Name)
                        .FirstOrDefault(),
                })
                .Take(10)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            birthdays = birthdayRows
                .Select(r => new BirthdayDto(
                    $"{r.FirstName} {r.LastName}", r.ClassName, today.Year - r.DateOfBirth.Year))
                .OrderBy(b => b.Name)
                .ToList();
        }

        var outstanding = currentYearId is { } feeYearId
            ? await ComputeOutstandingAsync(feeYearId, cancellationToken).ConfigureAwait(false)
            : 0m;

        var tenant = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenantContext.TenantId)
            .Select(t => new { t.SmsCredits, t.SubscriptionExpiresOn })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var marked = todayMarks?.Marked ?? 0;
        var present = todayMarks?.Present ?? 0;
        return new DashboardDto(
            activeStudents,
            marked,
            present,
            marked == 0 ? 0 : Math.Round(present * 100m / marked, 1),
            feesThisMonth,
            overdueLoans,
            pendingLeave,
            unreadMessages,
            openEnquiries,
            followUpsDue,
            upcomingExams,
            attendanceTrend,
            feeTrend,
            birthdays,
            outstanding,
            tenant?.SmsCredits ?? 0,
            tenant?.SubscriptionExpiresOn);
    }

    /// <summary>Base fees still owed: structure − concessions − paid, floored per student.</summary>
    private async Task<decimal> ComputeOutstandingAsync(Guid yearId, CancellationToken ct)
    {
        var dueByClass = await _db.FeeStructureItems.AsNoTracking()
            .Where(i => i.AcademicYearId == yearId)
            .GroupBy(i => i.SchoolClassId)
            .Select(g => new { ClassId = g.Key, Due = g.Sum(i => i.Amount) })
            .ToDictionaryAsync(g => g.ClassId, g => g.Due, ct)
            .ConfigureAwait(false);
        var enrollments = await _db.Enrollments.AsNoTracking()
            .Where(e => e.AcademicYearId == yearId && e.Status == EnrollmentStatus.Active)
            .Select(e => new { e.StudentId, e.SchoolClassId })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var paid = await _db.FeePayments.AsNoTracking()
            .Where(p => p.AcademicYearId == yearId)
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(g => g.StudentId, g => g.Total, ct)
            .ConfigureAwait(false);
        var concessions = await _db.FeeConcessions.AsNoTracking()
            .Where(c => c.AcademicYearId == yearId)
            .GroupBy(c => c.StudentId)
            .Select(g => new { StudentId = g.Key, Total = g.Sum(c => c.Amount) })
            .ToDictionaryAsync(g => g.StudentId, g => g.Total, ct)
            .ConfigureAwait(false);

        return enrollments.Sum(e => Math.Max(0m,
            dueByClass.GetValueOrDefault(e.SchoolClassId)
            - concessions.GetValueOrDefault(e.StudentId)
            - paid.GetValueOrDefault(e.StudentId)));
    }
}
