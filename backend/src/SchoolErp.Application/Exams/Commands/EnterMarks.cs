using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Exams.Commands;

/// <summary>One student's marks in a submission.</summary>
public sealed record MarkInput(Guid EnrollmentId, decimal? MarksObtained, bool IsAbsent);

/// <summary>
/// Enters (or corrects) marks for one paper. Upserts one row per enrollment;
/// rejected once the exam is published.
/// </summary>
public sealed record EnterMarksCommand(
    Guid ExamSubjectId, IReadOnlyList<MarkInput> Entries) : IRequest<int>;

/// <summary>Marks shape rules; range check against MaxMarks happens in the handler.</summary>
public sealed class EnterMarksCommandValidator : AbstractValidator<EnterMarksCommand>
{
    public EnterMarksCommandValidator()
    {
        RuleFor(c => c.Entries).NotEmpty()
            .Must(e => e.Select(x => x.EnrollmentId).Distinct().Count() == e.Count)
            .WithMessage("Each enrollment may appear only once.");

        RuleForEach(c => c.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e).Must(e => e.IsAbsent || e.MarksObtained.HasValue)
                .WithMessage("Provide marks or mark the student absent.");
            entry.RuleFor(e => e.MarksObtained).GreaterThanOrEqualTo(0)
                .When(e => e.MarksObtained.HasValue);
        });
    }
}

/// <summary>Upserts mark rows with class-membership and range validation.</summary>
public sealed class EnterMarksCommandHandler : IRequestHandler<EnterMarksCommand, int>
{
    private readonly IApplicationDbContext _db;

    public EnterMarksCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<int> Handle(EnterMarksCommand request, CancellationToken cancellationToken)
    {
        var paper = await _db.ExamSubjects
            .Include(s => s.Subject)
            .FirstOrDefaultAsync(s => s.Id == request.ExamSubjectId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(ExamSubject), request.ExamSubjectId);

        var examPublished = await _db.Exams
            .AnyAsync(e => e.Id == paper.ExamId && e.Status == ExamStatus.Published, cancellationToken)
            .ConfigureAwait(false);
        if (examPublished)
        {
            throw new ConflictException("Marks are frozen: the exam has been published.");
        }

        var overMax = request.Entries
            .FirstOrDefault(e => e.MarksObtained.HasValue && e.MarksObtained.Value > paper.MaxMarks);
        if (overMax is not null)
        {
            throw new ConflictException(
                $"Marks {overMax.MarksObtained} exceed the paper maximum of {paper.MaxMarks}.");
        }

        // Only active placements of the paper's class may receive marks.
        var enrollments = await _db.Enrollments
            .Where(e => e.SchoolClassId == paper.SchoolClassId && e.Status == EnrollmentStatus.Active)
            .Select(e => new { e.Id, e.StudentId })
            .ToDictionaryAsync(e => e.Id, e => e.StudentId, cancellationToken)
            .ConfigureAwait(false);

        var unknown = request.Entries.FirstOrDefault(e => !enrollments.ContainsKey(e.EnrollmentId));
        if (unknown is not null)
        {
            throw new NotFoundException("Enrollment (in this paper's class)", unknown.EnrollmentId);
        }

        var existing = await _db.MarkEntries
            .Where(m => m.ExamSubjectId == paper.Id)
            .ToDictionaryAsync(m => m.EnrollmentId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var entry in request.Entries)
        {
            if (existing.TryGetValue(entry.EnrollmentId, out var row))
            {
                row.MarksObtained = entry.IsAbsent ? null : entry.MarksObtained;
                row.IsAbsent = entry.IsAbsent;
            }
            else
            {
                _db.MarkEntries.Add(new MarkEntry
                {
                    ExamSubjectId = paper.Id,
                    EnrollmentId = entry.EnrollmentId,
                    StudentId = enrollments[entry.EnrollmentId],
                    MarksObtained = entry.IsAbsent ? null : entry.MarksObtained,
                    IsAbsent = entry.IsAbsent,
                });
            }
        }

        return await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>The marks-entry grid for one paper.</summary>
public sealed record GetMarksGridQuery(Guid ExamSubjectId) : IRequest<MarksGridDto>;

/// <summary>Roster of the paper's class with any marks already entered.</summary>
public sealed class GetMarksGridQueryHandler : IRequestHandler<GetMarksGridQuery, MarksGridDto>
{
    private readonly IApplicationDbContext _db;

    public GetMarksGridQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<MarksGridDto> Handle(GetMarksGridQuery request, CancellationToken cancellationToken)
    {
        var paper = await _db.ExamSubjects.AsNoTracking()
            .Include(s => s.Subject)
            .Include(s => s.SchoolClass)
            .FirstOrDefaultAsync(s => s.Id == request.ExamSubjectId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(ExamSubject), request.ExamSubjectId);

        var rows = await _db.Enrollments.AsNoTracking()
            .Where(e => e.SchoolClassId == paper.SchoolClassId && e.Status == EnrollmentStatus.Active)
            .Select(e => new
            {
                e.Id,
                e.StudentId,
                e.RollNumber,
                Student = _db.Students.Where(s => s.Id == e.StudentId)
                    .Select(s => new { s.FirstName, s.LastName, s.AdmissionNumber })
                    .First(),
                Mark = _db.MarkEntries
                    .Where(m => m.ExamSubjectId == paper.Id && m.EnrollmentId == e.Id)
                    .Select(m => new { m.MarksObtained, m.IsAbsent })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new MarksGridDto
        {
            ExamSubjectId = paper.Id,
            SubjectName = paper.Subject!.Name,
            ClassName = paper.SchoolClass!.Name,
            MaxMarks = paper.MaxMarks,
            Rows = rows
                .Select(r => new MarksGridRowDto
                {
                    EnrollmentId = r.Id,
                    StudentId = r.StudentId,
                    StudentName = $"{r.Student.FirstName} {r.Student.LastName}".Trim(),
                    AdmissionNumber = r.Student.AdmissionNumber,
                    RollNumber = r.RollNumber,
                    MarksObtained = r.Mark?.MarksObtained,
                    IsAbsent = r.Mark?.IsAbsent ?? false,
                })
                .OrderBy(r => r.RollNumber ?? int.MaxValue).ThenBy(r => r.StudentName)
                .ToList(),
        };
    }
}
