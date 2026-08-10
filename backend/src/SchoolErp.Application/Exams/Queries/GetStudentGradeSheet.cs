using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Exams.Queries;

/// <summary>One paper on a grade sheet.</summary>
public sealed record GradeSheetPaperDto(
    string SubjectName,
    int Credits,
    decimal? Percent,
    string Grade,
    int GradePoint,
    bool IsAbsent);

/// <summary>One semester's result: its papers and the SGPA they produce.</summary>
public sealed record SemesterResultDto(
    Guid ExamId,
    string ExamName,
    string CohortName,
    DateOnly EndDate,
    decimal? Sgpa,
    int CreditsEarned,
    int CreditsAttempted,
    IReadOnlyList<GradeSheetPaperDto> Papers);

/// <summary>
/// A college student's cumulative record: every published semester, its SGPA,
/// and the CGPA across all of them.
/// </summary>
public sealed record GradeSheetDto(
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    string? ProgrammeName,
    decimal? Cgpa,
    int CreditsEarned,
    int CreditsAttempted,
    IReadOnlyList<SemesterResultDto> Semesters,
    string? Unavailable);

/// <summary>The cumulative grade sheet for one student.</summary>
public sealed record GetStudentGradeSheetQuery(Guid StudentId) : IRequest<GradeSheetDto>;

/// <summary>
/// Builds the transcript from PUBLISHED exams only — draft marks are still
/// being entered and are not results. Everything is credit-weighted through
/// <see cref="CbcsGradeCalculator"/>; a semester whose papers carry no credits
/// reports a null SGPA rather than a zero.
/// </summary>
public sealed class GetStudentGradeSheetQueryHandler
    : IRequestHandler<GetStudentGradeSheetQuery, GradeSheetDto>
{
    private readonly IApplicationDbContext _db;

    public GetStudentGradeSheetQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<GradeSheetDto> Handle(
        GetStudentGradeSheetQuery request, CancellationToken cancellationToken)
    {
        var student = await _db.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        // The programme they are currently on. A transfer mid-course shows the
        // programme they are in now; the per-semester rows still name the
        // cohort each result was earned in.
        var programmeName = await _db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == student.Id &&
                        e.Status == EnrollmentStatus.Active &&
                        e.ProgrammeId != null)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => _db.Programmes
                .Where(p => p.Id == e.ProgrammeId)
                .Select(p => p.Name)
                .FirstOrDefault())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // The institution's own ordinance, or the UGC scale where none is set.
        // Loaded once and threaded through every grade on the sheet: reading
        // it per paper would let a mid-request change split one transcript
        // across two scales.
        var ownBands = await _db.GradeBands.AsNoTracking()
            .Select(b => new { b.MinPercent, b.Letter, b.Point })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var scale = ownBands.Count > 0
            ? ownBands.Select(b => (b.MinPercent, b.Letter, b.Point)).ToList()
            : CbcsGradeCalculator.UgcDefault.ToList();

        // ExamSubject has no Exam navigation, so the published set is resolved
        // first and the marks filtered against it.
        var publishedExams = await _db.Exams.AsNoTracking()
            .Where(e => e.Status == ExamStatus.Published)
            .Select(e => new { e.Id, e.Name, e.EndDate })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var publishedIds = publishedExams.Select(e => e.Id).ToList();

        var rawMarks = await _db.MarkEntries.AsNoTracking()
            .Where(m => m.StudentId == student.Id &&
                        publishedIds.Contains(m.ExamSubject!.ExamId))
            .Select(m => new
            {
                m.ExamSubject!.ExamId,
                CohortName = m.ExamSubject.SchoolClass!.Name,
                SubjectName = m.ExamSubject.Subject!.Name,
                m.ExamSubject.Credits,
                m.ExamSubject.MaxMarks,
                m.MarksObtained,
                m.IsAbsent,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var examsById = publishedExams.ToDictionary(e => e.Id);
        var marks = rawMarks
            .Select(m => new
            {
                m.ExamId,
                ExamName = examsById[m.ExamId].Name,
                EndDate = examsById[m.ExamId].EndDate,
                m.CohortName,
                m.SubjectName,
                m.Credits,
                m.MaxMarks,
                m.MarksObtained,
                m.IsAbsent,
            })
            .ToList();

        var semesters = marks
            .GroupBy(m => new { m.ExamId, m.ExamName, m.EndDate, m.CohortName })
            .OrderBy(g => g.Key.EndDate)
            .Select(group =>
            {
                var papers = group
                    .OrderBy(m => m.SubjectName, StringComparer.OrdinalIgnoreCase)
                    .Select(m =>
                    {
                        var percent = m.IsAbsent || m.MarksObtained is null
                            ? (decimal?)null
                            : GradeCalculator.Percent(m.MarksObtained.Value, m.MaxMarks);
                        var grade = CbcsGradeCalculator.GradeFor(
                            percent ?? 0m, scale, m.IsAbsent);
                        return new GradeSheetPaperDto(
                            m.SubjectName, m.Credits ?? 0, percent,
                            grade.Letter, grade.Point, m.IsAbsent);
                    })
                    .ToList();

                return new SemesterResultDto(
                    group.Key.ExamId,
                    group.Key.ExamName,
                    group.Key.CohortName,
                    group.Key.EndDate,
                    CbcsGradeCalculator.Gpa(ToCredited(papers), scale),
                    CreditsEarned: papers.Where(Passed).Sum(p => p.Credits),
                    CreditsAttempted: papers.Sum(p => p.Credits),
                    papers);
            })
            .ToList();

        var allPapers = semesters.SelectMany(s => s.Papers).ToList();

        return new GradeSheetDto(
            student.Id,
            student.FullName,
            student.AdmissionNumber,
            programmeName,
            CbcsGradeCalculator.Gpa(ToCredited(allPapers), scale),
            CreditsEarned: allPapers.Where(Passed).Sum(p => p.Credits),
            CreditsAttempted: allPapers.Sum(p => p.Credits),
            semesters,
            Unavailable: DescribeGap(semesters, allPapers));
    }

    /// <summary>A paper is earned only if it was passed; points decide it.</summary>
    private static bool Passed(GradeSheetPaperDto paper) => paper.GradePoint > 0;

    private static IEnumerable<CreditedPaper> ToCredited(IEnumerable<GradeSheetPaperDto> papers) =>
        papers.Select(p => new CreditedPaper(p.Credits, p.Percent ?? 0m, p.IsAbsent));

    /// <summary>
    /// Says why there is no CGPA, instead of leaving the caller to guess from
    /// a null. The two reasons are entirely different problems.
    /// </summary>
    private static string? DescribeGap(
        IReadOnlyList<SemesterResultDto> semesters, IReadOnlyList<GradeSheetPaperDto> papers)
    {
        if (semesters.Count == 0)
        {
            return "No published results yet.";
        }

        return papers.All(p => p.Credits <= 0)
            ? "No paper carries credits, so no GPA can be calculated. " +
              "Set credits on each paper when scheduling it."
            : null;
    }
}
