using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Homework;

namespace SchoolErp.Application.Homework;

/// <summary>Homework projection.</summary>
public sealed record HomeworkDto(
    Guid Id,
    string ClassName,
    string? SectionName,
    string SubjectName,
    string Title,
    string Instructions,
    DateOnly AssignedOn,
    DateOnly DueDate);

/// <summary>Posts homework for a class (optionally one section).</summary>
public sealed record CreateHomeworkCommand(
    Guid SchoolClassId,
    Guid? SectionId,
    Guid SubjectId,
    string Title,
    string Instructions,
    DateOnly DueDate) : IRequest<Guid>;

/// <summary>Homework shape rules.</summary>
public sealed class CreateHomeworkCommandValidator : AbstractValidator<CreateHomeworkCommand>
{
    public CreateHomeworkCommandValidator(TimeProvider clock)
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Instructions).NotEmpty().MaximumLength(4000);
        RuleFor(c => c.DueDate)
            .Must(d => d >= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddDays(-1)))
            .WithMessage("Due date cannot be in the past.");
    }
}

/// <summary>Creates the assignment after reference checks.</summary>
public sealed class CreateHomeworkCommandHandler : IRequestHandler<CreateHomeworkCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public CreateHomeworkCommandHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Guid> Handle(CreateHomeworkCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.SchoolClasses.AnyAsync(c => c.Id == request.SchoolClassId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("SchoolClass", request.SchoolClassId);
        }

        if (request.SectionId is { } sectionId &&
            !await _db.Sections.AnyAsync(
                    s => s.Id == sectionId && s.SchoolClassId == request.SchoolClassId,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("Section (in this class)", sectionId);
        }

        if (!await _db.Subjects.AnyAsync(s => s.Id == request.SubjectId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("Subject", request.SubjectId);
        }

        var homework = new HomeworkAssignment
        {
            SchoolClassId = request.SchoolClassId,
            SectionId = request.SectionId,
            SubjectId = request.SubjectId,
            Title = request.Title.Trim(),
            Instructions = request.Instructions.Trim(),
            AssignedOn = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime),
            DueDate = request.DueDate,
        };
        _db.HomeworkAssignments.Add(homework);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return homework.Id;
    }
}

/// <summary>Recent homework of a class (staff view).</summary>
public sealed record GetClassHomeworkQuery(Guid SchoolClassId, int Limit = 50)
    : IRequest<IReadOnlyList<HomeworkDto>>;

/// <summary>Newest first.</summary>
public sealed class GetClassHomeworkQueryHandler
    : IRequestHandler<GetClassHomeworkQuery, IReadOnlyList<HomeworkDto>>
{
    private readonly IApplicationDbContext _db;

    public GetClassHomeworkQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<HomeworkDto>> Handle(
        GetClassHomeworkQuery request, CancellationToken cancellationToken) =>
        await _db.HomeworkAssignments.AsNoTracking()
            .Where(h => h.SchoolClassId == request.SchoolClassId)
            .OrderByDescending(h => h.AssignedOn).ThenByDescending(h => h.CreatedAt)
            .Take(Math.Clamp(request.Limit, 1, 200))
            .Select(h => new HomeworkDto(
                h.Id, h.SchoolClass!.Name,
                h.SectionId != null
                    ? _db.Sections.Where(s => s.Id == h.SectionId).Select(s => s.Name).FirstOrDefault()
                    : null,
                h.Subject!.Name, h.Title, h.Instructions, h.AssignedOn, h.DueDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>
/// Homework for one student: their current class, whole-class items plus
/// those targeted at their section.
/// </summary>
public sealed record GetStudentHomeworkQuery(Guid StudentId)
    : IRequest<IReadOnlyList<HomeworkDto>>;

/// <summary>Resolves the student's placement, then filters.</summary>
public sealed class GetStudentHomeworkQueryHandler
    : IRequestHandler<GetStudentHomeworkQuery, IReadOnlyList<HomeworkDto>>
{
    private readonly IApplicationDbContext _db;

    public GetStudentHomeworkQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<HomeworkDto>> Handle(
        GetStudentHomeworkQuery request, CancellationToken cancellationToken)
    {
        var placement = await _db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == request.StudentId && e.AcademicYear!.IsCurrent)
            .Select(e => new { e.SchoolClassId, e.SectionId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (placement is null)
        {
            return [];
        }

        return await _db.HomeworkAssignments.AsNoTracking()
            .Where(h => h.SchoolClassId == placement.SchoolClassId &&
                        (h.SectionId == null || h.SectionId == placement.SectionId))
            .OrderByDescending(h => h.DueDate)
            .Take(50)
            .Select(h => new HomeworkDto(
                h.Id, h.SchoolClass!.Name, null,
                h.Subject!.Name, h.Title, h.Instructions, h.AssignedOn, h.DueDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
