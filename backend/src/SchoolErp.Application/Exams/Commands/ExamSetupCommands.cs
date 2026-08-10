using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Exams;

namespace SchoolErp.Application.Exams.Commands;

/// <summary>Creates a subject.</summary>
public sealed record CreateSubjectCommand(string Name, string Code) : IRequest<SubjectDto>;

/// <summary>Subject shape rules.</summary>
public sealed class CreateSubjectCommandValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(16)
            .Matches("^[A-Z0-9-]+$").WithMessage("Code must be uppercase letters, digits or hyphens.");
    }
}

/// <summary>Creates the subject after per-tenant uniqueness checks.</summary>
public sealed class CreateSubjectCommandHandler : IRequestHandler<CreateSubjectCommand, SubjectDto>
{
    private readonly IApplicationDbContext _db;

    public CreateSubjectCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<SubjectDto> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();

        if (await _db.Subjects.AnyAsync(s => s.Name == name || s.Code == code, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ConflictException($"A subject with this name or code already exists.");
        }

        var subject = new Subject { Name = name, Code = code };
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return subject.ToDto();
    }
}

/// <summary>Lists subjects alphabetically.</summary>
public sealed record GetSubjectsQuery : IRequest<IReadOnlyList<SubjectDto>>;

/// <summary>Simple projection query.</summary>
public sealed class GetSubjectsQueryHandler : IRequestHandler<GetSubjectsQuery, IReadOnlyList<SubjectDto>>
{
    private readonly IApplicationDbContext _db;

    public GetSubjectsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SubjectDto>> Handle(
        GetSubjectsQuery request, CancellationToken cancellationToken) =>
        await _db.Subjects.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, s.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>Creates an exam for an academic year.</summary>
public sealed record CreateExamCommand(
    string Name, Guid AcademicYearId, DateOnly StartDate, DateOnly EndDate) : IRequest<Guid>;

/// <summary>Exam shape rules.</summary>
public sealed class CreateExamCommandValidator : AbstractValidator<CreateExamCommand>
{
    public CreateExamCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.EndDate).GreaterThanOrEqualTo(c => c.StartDate);
    }
}

/// <summary>Creates the exam in Draft.</summary>
public sealed class CreateExamCommandHandler : IRequestHandler<CreateExamCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateExamCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.AcademicYears.AnyAsync(y => y.Id == request.AcademicYearId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException(nameof(AcademicYear), request.AcademicYearId);
        }

        var name = request.Name.Trim();
        if (await _db.Exams.AnyAsync(
                e => e.AcademicYearId == request.AcademicYearId && e.Name == name, cancellationToken)
            .ConfigureAwait(false))
        {
            throw new ConflictException($"Exam '{name}' already exists in this academic year.");
        }

        var exam = new Exam
        {
            Name = name,
            AcademicYearId = request.AcademicYearId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
        };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return exam.Id;
    }
}

/// <summary>Schedules a paper (subject for a class) inside an exam.</summary>
public sealed record ScheduleExamSubjectCommand(
    Guid ExamId,
    Guid SchoolClassId,
    Guid SubjectId,
    DateOnly? ExamDate,
    decimal MaxMarks,
    decimal PassMarks,
    // Credit weight, for colleges on the CBCS system. Null at a school.
    int? Credits = null) : IRequest<Guid>;

/// <summary>Paper shape rules.</summary>
public sealed class ScheduleExamSubjectCommandValidator : AbstractValidator<ScheduleExamSubjectCommand>
{
    public ScheduleExamSubjectCommandValidator()
    {
        RuleFor(c => c.MaxMarks).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(c => c.PassMarks).GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(c => c.MaxMarks)
            .WithMessage("Pass marks cannot exceed maximum marks.");

        // Indian degree papers run 1–6 credits; anything outside that is a
        // typo, and a wrong credit silently skews every GPA it touches.
        RuleFor(c => c.Credits!.Value).InclusiveBetween(1, 12)
            .When(c => c.Credits is not null)
            .WithMessage("Credits must be between 1 and 12.");
    }
}

/// <summary>Adds the paper after existence and duplication checks.</summary>
public sealed class ScheduleExamSubjectCommandHandler
    : IRequestHandler<ScheduleExamSubjectCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public ScheduleExamSubjectCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(
        ScheduleExamSubjectCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Exam), request.ExamId);

        if (exam.Status == ExamStatus.Published)
        {
            throw new ConflictException("A published exam cannot be modified.");
        }

        if (!await _db.SchoolClasses.AnyAsync(c => c.Id == request.SchoolClassId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException(nameof(SchoolClass), request.SchoolClassId);
        }

        if (!await _db.Subjects.AnyAsync(s => s.Id == request.SubjectId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException(nameof(Subject), request.SubjectId);
        }

        var duplicate = await _db.ExamSubjects.AnyAsync(
                s => s.ExamId == request.ExamId &&
                     s.SchoolClassId == request.SchoolClassId &&
                     s.SubjectId == request.SubjectId,
                cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            throw new ConflictException("This subject is already scheduled for this class.");
        }

        var paper = new ExamSubject
        {
            ExamId = request.ExamId,
            SchoolClassId = request.SchoolClassId,
            SubjectId = request.SubjectId,
            ExamDate = request.ExamDate,
            MaxMarks = request.MaxMarks,
            PassMarks = request.PassMarks,
            Credits = request.Credits,
        };
        _db.ExamSubjects.Add(paper);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return paper.Id;
    }
}

/// <summary>Lists exams of a year with their scheduled papers.</summary>
public sealed record GetExamsQuery(Guid AcademicYearId) : IRequest<IReadOnlyList<ExamDto>>;

/// <summary>Projection including papers.</summary>
public sealed class GetExamsQueryHandler : IRequestHandler<GetExamsQuery, IReadOnlyList<ExamDto>>
{
    private readonly IApplicationDbContext _db;

    public GetExamsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ExamDto>> Handle(
        GetExamsQuery request, CancellationToken cancellationToken)
    {
        return await _db.Exams.AsNoTracking()
            .Where(e => e.AcademicYearId == request.AcademicYearId)
            .OrderBy(e => e.StartDate)
            .Select(ExamMappings.ExamProjection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
