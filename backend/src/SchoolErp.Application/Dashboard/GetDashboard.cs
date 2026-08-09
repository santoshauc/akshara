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
    IReadOnlyList<UpcomingExamDto> UpcomingExams);

/// <summary>The staff dashboard tiles.</summary>
public sealed record GetDashboardQuery : IRequest<DashboardDto>;

/// <summary>Aggregates the hot numbers; every query is tenant-filtered.</summary>
public sealed class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public GetDashboardQueryHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
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
            upcomingExams);
    }
}
