using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.Exams;

namespace SchoolErp.Application.Insights;

/// <summary>
/// One teacher's teaching-outcome numbers. Averages are null until any of
/// their subjects' papers are published.
/// </summary>
public sealed record TeacherInsightDto(
    Guid TeacherId,
    string Name,
    int PeriodsPerWeek,
    decimal? AveragePercent,
    decimal? DeltaVsSchool,
    int DaysAbsent,
    int MarksBacklog);

/// <summary>Teaching outcomes per active teacher, tenant-scoped.</summary>
public sealed record GetTeacherInsightsQuery : IRequest<List<TeacherInsightDto>>;

/// <summary>
/// Correlates each teacher's timetable (subject + class pairs they teach)
/// with published exam results of those same papers. The school delta
/// compares against the school-wide average of the same exams, so a hard
/// exam doesn't read as a weak teacher.
/// </summary>
public sealed class GetTeacherInsightsQueryHandler
    : IRequestHandler<GetTeacherInsightsQuery, List<TeacherInsightDto>>
{
    private readonly IApplicationDbContext _db;

    public GetTeacherInsightsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<TeacherInsightDto>> Handle(
        GetTeacherInsightsQuery request, CancellationToken cancellationToken)
    {
        var currentYearId = await _db.AcademicYears.AsNoTracking()
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var teachers = await _db.Teachers.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.FullName)
            .Select(t => new { t.Id, t.FullName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (teachers.Count == 0)
        {
            return [];
        }

        var slots = await _db.TimetableEntries.AsNoTracking()
            .Where(e => e.IsPublished && e.TeacherId != null)
            .Select(e => new { e.TeacherId, e.SubjectId, e.SchoolClassId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Per published paper of the current year: average % and its exam.
        var paperStats = currentYearId is { } yearId
            ? await (
                    from s in _db.ExamSubjects.AsNoTracking()
                    join x in _db.Exams.AsNoTracking() on s.ExamId equals x.Id
                    where x.Status == ExamStatus.Published && x.AcademicYearId == yearId &&
                          s.MaxMarks > 0
                    join m in _db.MarkEntries.AsNoTracking() on s.Id equals m.ExamSubjectId
                    where !m.IsAbsent && m.MarksObtained != null
                    group new { m, s } by new { s.ExamId, s.SubjectId, s.SchoolClassId } into g
                    select new
                    {
                        g.Key.ExamId,
                        g.Key.SubjectId,
                        g.Key.SchoolClassId,
                        Average = g.Average(p => p.m.MarksObtained!.Value * 100m / p.s.MaxMarks),
                    })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : [];

        // Papers still awaiting any marks (draft or published), current year.
        var pendingPapers = currentYearId is { } backlogYearId
            ? await (
                    from s in _db.ExamSubjects.AsNoTracking()
                    join x in _db.Exams.AsNoTracking() on s.ExamId equals x.Id
                    where x.AcademicYearId == backlogYearId &&
                          !_db.MarkEntries.Any(m => m.ExamSubjectId == s.Id)
                    select new { s.SubjectId, s.SchoolClassId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : [];

        var absences = await _db.TimetableSubstitutions.AsNoTracking()
            .GroupBy(s => s.AbsentTeacherId)
            .Select(g => new { TeacherId = g.Key, Days = g.Select(s => s.Date).Distinct().Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<TeacherInsightDto>(teachers.Count);
        foreach (var teacher in teachers)
        {
            var taught = slots
                .Where(s => s.TeacherId == teacher.Id)
                .ToList();
            var taughtPairs = taught
                .Select(s => (s.SubjectId, s.SchoolClassId))
                .ToHashSet();

            var ownPapers = paperStats
                .Where(p => taughtPairs.Contains((p.SubjectId, p.SchoolClassId)))
                .ToList();
            decimal? average = ownPapers.Count > 0
                ? Math.Round(ownPapers.Average(p => p.Average), 1)
                : null;

            decimal? delta = null;
            if (average is { } avg)
            {
                var ownExamIds = ownPapers.Select(p => p.ExamId).ToHashSet();
                var schoolAverage = paperStats
                    .Where(p => ownExamIds.Contains(p.ExamId))
                    .Average(p => p.Average);
                delta = Math.Round(avg - schoolAverage, 1);
            }

            result.Add(new TeacherInsightDto(
                teacher.Id,
                teacher.FullName,
                taught.Count,
                average,
                delta,
                absences.FirstOrDefault(a => a.TeacherId == teacher.Id)?.Days ?? 0,
                pendingPapers.Count(p => taughtPairs.Contains((p.SubjectId, p.SchoolClassId)))));
        }

        return result;
    }
}
