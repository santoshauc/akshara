using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Insights;

/// <summary>One subject: the child's % beside the section average.</summary>
public sealed record SubjectComparisonDto(
    string Subject, decimal ChildPercent, decimal ClassAverage);

/// <summary>
/// How one child compares with their section — always against aggregates,
/// never another named student.
/// </summary>
public sealed record StudentInsightsDto(
    string? ExamName,
    List<SubjectComparisonDto> Subjects,
    int? Rank,
    int? SectionSize,
    decimal? ChildAttendancePercent,
    decimal? ClassAttendancePercent);

/// <summary>Peer comparison for one student (latest published exam + this month).</summary>
public sealed record GetStudentInsightsQuery(Guid StudentId) : IRequest<StudentInsightsDto>;

/// <summary>
/// Compares against section peers only: subject averages and rank come from
/// the latest published exam with marks for the child's class; attendance
/// compares this month's daily roll-call.
/// </summary>
public sealed class GetStudentInsightsQueryHandler
    : IRequestHandler<GetStudentInsightsQuery, StudentInsightsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public GetStudentInsightsQueryHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<StudentInsightsDto> Handle(
        GetStudentInsightsQuery request, CancellationToken cancellationToken)
    {
        var currentYearId = await _db.AcademicYears.AsNoTracking()
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var enrollment = currentYearId is { } yearId
            ? await _db.Enrollments.AsNoTracking()
                .Where(e => e.StudentId == request.StudentId &&
                            e.AcademicYearId == yearId &&
                            e.Status == EnrollmentStatus.Active)
                .Select(e => new { e.SectionId, e.SchoolClassId })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;
        if (enrollment is null)
        {
            throw new NotFoundException(nameof(Student), request.StudentId);
        }

        var (examName, subjects, rank, sectionSize) = await CompareExamAsync(
            request.StudentId, currentYearId!.Value, enrollment.SchoolClassId,
            enrollment.SectionId, cancellationToken).ConfigureAwait(false);

        var (childAttendance, classAttendance) = await CompareAttendanceAsync(
            request.StudentId, enrollment.SectionId, cancellationToken).ConfigureAwait(false);

        return new StudentInsightsDto(
            examName, subjects, rank, sectionSize, childAttendance, classAttendance);
    }

    private async Task<(string? ExamName, List<SubjectComparisonDto> Subjects, int? Rank, int? Size)>
        CompareExamAsync(Guid studentId, Guid yearId, Guid classId, Guid sectionId, CancellationToken ct)
    {
        // Latest published exam that actually has marks for this class.
        var exam = await (
                from x in _db.Exams.AsNoTracking()
                where x.Status == ExamStatus.Published && x.AcademicYearId == yearId
                join s in _db.ExamSubjects.AsNoTracking() on x.Id equals s.ExamId
                where s.SchoolClassId == classId &&
                      _db.MarkEntries.Any(m => m.ExamSubjectId == s.Id)
                orderby x.StartDate descending
                select new { x.Id, x.Name })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (exam is null)
        {
            return (null, [], null, null);
        }

        // Every section peer's marks for that exam, subject by subject.
        var rows = await (
                from m in _db.MarkEntries.AsNoTracking()
                where !m.IsAbsent && m.MarksObtained != null
                join s in _db.ExamSubjects.AsNoTracking() on m.ExamSubjectId equals s.Id
                where s.ExamId == exam.Id && s.SchoolClassId == classId && s.MaxMarks > 0
                join e in _db.Enrollments.AsNoTracking() on m.EnrollmentId equals e.Id
                where e.SectionId == sectionId
                join sub in _db.Subjects.AsNoTracking() on s.SubjectId equals sub.Id
                select new
                {
                    m.StudentId,
                    Subject = sub.Name,
                    Percent = m.MarksObtained!.Value * 100m / s.MaxMarks,
                    Marks = m.MarksObtained.Value,
                })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var subjects = rows
            .GroupBy(r => r.Subject)
            .Where(g => g.Any(r => r.StudentId == studentId))
            .Select(g => new SubjectComparisonDto(
                g.Key,
                Math.Round(g.First(r => r.StudentId == studentId).Percent, 1),
                Math.Round(g.Average(r => r.Percent), 1)))
            .OrderBy(s => s.Subject)
            .ToList();

        var totals = rows
            .GroupBy(r => r.StudentId)
            .Select(g => new { StudentId = g.Key, Total = g.Sum(r => r.Marks) })
            .ToList();
        var own = totals.FirstOrDefault(t => t.StudentId == studentId);
        int? rank = own is null ? null : 1 + totals.Count(t => t.Total > own.Total);

        return (exam.Name, subjects, rank, totals.Count == 0 ? null : totals.Count);
    }

    private async Task<(decimal? Child, decimal? Class)> CompareAttendanceAsync(
        Guid studentId, Guid sectionId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var perStudent = await _db.AttendanceRecords.AsNoTracking()
            .Where(a => a.SectionId == sectionId && a.Period == null && a.Date >= monthStart)
            .GroupBy(a => a.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Marked = g.Count(),
                Present = g.Count(a => a.Status == AttendanceStatus.Present ||
                                       a.Status == AttendanceStatus.Late ||
                                       a.Status == AttendanceStatus.HalfDay),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (perStudent.Count == 0)
        {
            return (null, null);
        }

        var own = perStudent.FirstOrDefault(p => p.StudentId == studentId);
        decimal? child = own is null || own.Marked == 0
            ? null
            : Math.Round(own.Present * 100m / own.Marked, 1);
        var sectionAverage = Math.Round(
            perStudent.Where(p => p.Marked > 0)
                .Average(p => p.Present * 100m / p.Marked), 1);
        return (child, sectionAverage);
    }
}
