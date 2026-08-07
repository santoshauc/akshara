using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Exams.Queries;

/// <summary>
/// A student's computed result for one exam: per-subject grades, totals,
/// percentage, overall grade, and rank within the student's section.
/// </summary>
public sealed record GetStudentResultQuery(Guid StudentId, Guid ExamId) : IRequest<StudentResultDto>;

/// <summary>Composes the result and section rank in two queries.</summary>
public sealed class GetStudentResultQueryHandler
    : IRequestHandler<GetStudentResultQuery, StudentResultDto>
{
    private readonly IApplicationDbContext _db;

    public GetStudentResultQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<StudentResultDto> Handle(
        GetStudentResultQuery request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Exam), request.ExamId);

        var marks = await _db.MarkEntries.AsNoTracking()
            .Where(m => m.StudentId == request.StudentId && m.ExamSubject!.ExamId == exam.Id)
            .Select(m => new
            {
                SubjectName = m.ExamSubject!.Subject!.Name,
                m.ExamSubject.MaxMarks,
                m.ExamSubject.PassMarks,
                m.MarksObtained,
                m.IsAbsent,
                m.EnrollmentId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (marks.Count == 0)
        {
            throw new NotFoundException("Result (no marks for this exam)", request.StudentId);
        }

        var lines = marks
            .OrderBy(m => m.SubjectName)
            .Select(m =>
            {
                var obtained = m.IsAbsent ? 0 : m.MarksObtained ?? 0;
                return new ResultLineDto(
                    m.SubjectName,
                    m.MaxMarks,
                    m.IsAbsent ? null : m.MarksObtained,
                    m.IsAbsent,
                    m.IsAbsent ? "AB" : GradeCalculator.GradeFor(GradeCalculator.Percent(obtained, m.MaxMarks)),
                    !m.IsAbsent && obtained >= m.PassMarks);
            })
            .ToList();

        var totalMax = marks.Sum(m => m.MaxMarks);
        var totalObtained = marks.Sum(m => m.IsAbsent ? 0 : m.MarksObtained ?? 0);
        var percent = GradeCalculator.Percent(totalObtained, totalMax);

        var (rank, sectionSize) = await ComputeSectionRankAsync(
            request.StudentId, exam.Id, marks[0].EnrollmentId, totalObtained, cancellationToken)
            .ConfigureAwait(false);

        return new StudentResultDto
        {
            StudentId = request.StudentId,
            ExamId = exam.Id,
            ExamName = exam.Name,
            ExamStatus = exam.Status,
            Lines = lines,
            TotalMax = totalMax,
            TotalObtained = totalObtained,
            Percent = percent,
            OverallGrade = GradeCalculator.GradeFor(percent),
            SectionRank = rank,
            SectionSize = sectionSize,
        };
    }

    /// <summary>Ranks by total marks (desc) among section peers with marks in this exam.</summary>
    private async Task<(int? Rank, int SectionSize)> ComputeSectionRankAsync(
        Guid studentId, Guid examId, Guid enrollmentId, decimal ownTotal, CancellationToken ct)
    {
        var sectionId = await _db.Enrollments.AsNoTracking()
            .Where(e => e.Id == enrollmentId)
            .Select(e => e.SectionId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (sectionId == Guid.Empty)
        {
            return (null, 0);
        }

        var totals = await _db.MarkEntries.AsNoTracking()
            .Where(m => m.ExamSubject!.ExamId == examId &&
                        _db.Enrollments.Any(e => e.Id == m.EnrollmentId && e.SectionId == sectionId))
            .GroupBy(m => m.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Total = g.Sum(m => m.IsAbsent ? 0 : m.MarksObtained ?? 0),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var rank = 1 + totals.Count(t => t.StudentId != studentId && t.Total > ownTotal);
        return (rank, totals.Count);
    }
}
