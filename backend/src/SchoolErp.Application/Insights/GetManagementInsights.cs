using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.Admissions;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Insights;

/// <summary>One day on the attendance trend.</summary>
public sealed record TrendPointDto(DateOnly Date, decimal Percent, int Marked);

/// <summary>One month of fee collections ("Mar 2026").</summary>
public sealed record MonthFeePointDto(string Month, decimal Collected);

/// <summary>One class's roll-call percentage this month.</summary>
public sealed record ClassAttendanceDto(string ClassName, decimal Percent, int Marked);

/// <summary>Average achievement across one published exam's papers.</summary>
public sealed record ExamAverageDto(string ExamName, decimal AveragePercent, int Entries);

/// <summary>The admissions pipeline by stage.</summary>
public sealed record EnquiryFunnelDto(int New, int Contacted, int Visit, int Admitted, int Lost);

/// <summary>Everything the management insights page draws, tenant-scoped.</summary>
public sealed record ManagementInsightsDto(
    List<TrendPointDto> AttendanceTrend,
    List<MonthFeePointDto> FeeSeries,
    decimal FeesOutstanding,
    List<ClassAttendanceDto> ClassAttendance,
    List<ExamAverageDto> ExamAverages,
    EnquiryFunnelDto EnquiryFunnel,
    int SubstitutionsThisMonth);

/// <summary>The management dashboard's chart data.</summary>
public sealed record GetManagementInsightsQuery : IRequest<ManagementInsightsDto>;

/// <summary>
/// Aggregates the school's key series. Attendance uses daily roll-call rows
/// only (period marks excluded); exam averages use published exams of the
/// current year; outstanding is base fees (structure − concessions − paid,
/// floored per student) — late fines are a collection-time detail.
/// </summary>
public sealed class GetManagementInsightsQueryHandler
    : IRequestHandler<GetManagementInsightsQuery, ManagementInsightsDto>
{
    private static readonly string[] MonthNames =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public GetManagementInsightsQueryHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ManagementInsightsDto> Handle(
        GetManagementInsightsQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var currentYearId = await _db.AcademicYears.AsNoTracking()
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var trend = await LoadAttendanceTrendAsync(today, cancellationToken).ConfigureAwait(false);
        var feeSeries = await LoadFeeSeriesAsync(today, cancellationToken).ConfigureAwait(false);
        var outstanding = currentYearId is { } yearId
            ? await LoadOutstandingAsync(yearId, cancellationToken).ConfigureAwait(false)
            : 0m;
        var classAttendance = await LoadClassAttendanceAsync(monthStart, cancellationToken)
            .ConfigureAwait(false);
        var examAverages = currentYearId is { } examYearId
            ? await LoadExamAveragesAsync(examYearId, cancellationToken).ConfigureAwait(false)
            : [];
        var funnel = await LoadFunnelAsync(cancellationToken).ConfigureAwait(false);

        var substitutions = await _db.TimetableSubstitutions.AsNoTracking()
            .CountAsync(s => s.Date >= monthStart && s.Date < monthStart.AddMonths(1),
                cancellationToken)
            .ConfigureAwait(false);

        return new ManagementInsightsDto(
            trend, feeSeries, outstanding, classAttendance, examAverages, funnel, substitutions);
    }

    private async Task<List<TrendPointDto>> LoadAttendanceTrendAsync(
        DateOnly today, CancellationToken ct)
    {
        var from = today.AddDays(-29);
        var days = await _db.AttendanceRecords.AsNoTracking()
            .Where(a => a.Period == null && a.Date >= from && a.Date <= today)
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
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return days
            .Select(d => new TrendPointDto(
                d.Date, Math.Round(d.Present * 100m / d.Marked, 1), d.Marked))
            .ToList();
    }

    private async Task<List<MonthFeePointDto>> LoadFeeSeriesAsync(DateOnly today, CancellationToken ct)
    {
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);
        var raw = await _db.FeePayments.AsNoTracking()
            .Where(p => p.PaidOn >= firstMonth)
            .GroupBy(p => new { p.PaidOn.Year, p.PaidOn.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Collected = g.Sum(p => p.Amount) })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Fill quiet months with zero so the series always spans 6 points.
        var series = new List<MonthFeePointDto>(6);
        for (var month = firstMonth; month <= today; month = month.AddMonths(1))
        {
            var hit = raw.FirstOrDefault(r => r.Year == month.Year && r.Month == month.Month);
            series.Add(new MonthFeePointDto(
                $"{MonthNames[month.Month - 1]} {month.Year}", hit?.Collected ?? 0m));
        }

        return series;
    }

    private async Task<decimal> LoadOutstandingAsync(Guid yearId, CancellationToken ct)
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

        var paidByStudent = await _db.FeePayments.AsNoTracking()
            .Where(p => p.AcademicYearId == yearId)
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, Paid = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(g => g.StudentId, g => g.Paid, ct)
            .ConfigureAwait(false);

        var concessionByStudent = await _db.FeeConcessions.AsNoTracking()
            .Where(c => c.AcademicYearId == yearId)
            .GroupBy(c => c.StudentId)
            .Select(g => new { StudentId = g.Key, Amount = g.Sum(c => c.Amount) })
            .ToDictionaryAsync(g => g.StudentId, g => g.Amount, ct)
            .ConfigureAwait(false);

        return enrollments.Sum(e => Math.Max(0m,
            dueByClass.GetValueOrDefault(e.SchoolClassId)
            - concessionByStudent.GetValueOrDefault(e.StudentId)
            - paidByStudent.GetValueOrDefault(e.StudentId)));
    }

    private async Task<List<ClassAttendanceDto>> LoadClassAttendanceAsync(
        DateOnly monthStart, CancellationToken ct)
    {
        var rows = await (
                from a in _db.AttendanceRecords.AsNoTracking()
                where a.Period == null && a.Date >= monthStart
                join e in _db.Enrollments.AsNoTracking() on a.EnrollmentId equals e.Id
                join c in _db.SchoolClasses.AsNoTracking() on e.SchoolClassId equals c.Id
                group a by new { c.Name, c.DisplayOrder } into g
                orderby g.Key.DisplayOrder
                select new
                {
                    g.Key.Name,
                    Marked = g.Count(),
                    Present = g.Count(a => a.Status == AttendanceStatus.Present ||
                                           a.Status == AttendanceStatus.Late ||
                                           a.Status == AttendanceStatus.HalfDay),
                })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows
            .Select(r => new ClassAttendanceDto(
                r.Name, Math.Round(r.Present * 100m / r.Marked, 1), r.Marked))
            .ToList();
    }

    private async Task<List<ExamAverageDto>> LoadExamAveragesAsync(Guid yearId, CancellationToken ct)
    {
        var rows = await (
                from m in _db.MarkEntries.AsNoTracking()
                where !m.IsAbsent && m.MarksObtained != null
                join s in _db.ExamSubjects.AsNoTracking() on m.ExamSubjectId equals s.Id
                join x in _db.Exams.AsNoTracking() on s.ExamId equals x.Id
                where x.Status == ExamStatus.Published && x.AcademicYearId == yearId &&
                      s.MaxMarks > 0
                group new { m, s } by new { x.Name, x.StartDate } into g
                orderby g.Key.StartDate
                select new
                {
                    g.Key.Name,
                    Average = g.Average(p => p.m.MarksObtained!.Value * 100m / p.s.MaxMarks),
                    Entries = g.Count(),
                })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows
            .Select(r => new ExamAverageDto(r.Name, Math.Round(r.Average, 1), r.Entries))
            .ToList();
    }

    private async Task<EnquiryFunnelDto> LoadFunnelAsync(CancellationToken ct)
    {
        var counts = await _db.AdmissionEnquiries.AsNoTracking()
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        int Of(EnquiryStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;
        return new EnquiryFunnelDto(
            Of(EnquiryStatus.New), Of(EnquiryStatus.Contacted), Of(EnquiryStatus.Visit),
            Of(EnquiryStatus.Admitted), Of(EnquiryStatus.Lost));
    }
}
