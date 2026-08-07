using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Timetable;

namespace SchoolErp.Application.Timetable;

/// <summary>One period slot as shown in grids.</summary>
public sealed record TimetableEntryDto(
    Guid Id,
    int DayOfWeek,
    int Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid SubjectId,
    string SubjectName,
    string? TeacherName,
    bool IsPublished);

/// <summary>Input slot when defining a timetable.</summary>
public sealed record TimetableEntryInput(
    int DayOfWeek,
    int Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid SubjectId,
    string? TeacherName);

/// <summary>
/// Replaces the timetable for a class scope (whole class or one section).
/// New entries start UNPUBLISHED — parents keep seeing nothing (or the old
/// published version is gone and they see nothing) until Publish is called.
/// Full-replace keeps duplicate (day, period) slots structurally impossible.
/// </summary>
public sealed record DefineTimetableCommand(
    Guid SchoolClassId,
    Guid? SectionId,
    IReadOnlyList<TimetableEntryInput> Entries) : IRequest;

/// <summary>Timetable shape rules.</summary>
public sealed class DefineTimetableCommandValidator : AbstractValidator<DefineTimetableCommand>
{
    public DefineTimetableCommandValidator()
    {
        RuleFor(c => c.Entries).NotEmpty()
            .Must(entries => entries
                .Select(e => (e.DayOfWeek, e.Period))
                .Distinct()
                .Count() == entries.Count)
            .WithMessage("Each (day, period) slot may appear only once.");

        RuleForEach(c => c.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.DayOfWeek).InclusiveBetween(1, 7);
            entry.RuleFor(e => e.Period).InclusiveBetween(1, 12);
            entry.RuleFor(e => e.EndTime).GreaterThan(e => e.StartTime)
                .WithMessage("A period must end after it starts.");
            entry.RuleFor(e => e.TeacherName).MaximumLength(128);
        });
    }
}

/// <summary>Validates references then replaces the scope atomically.</summary>
public sealed class DefineTimetableCommandHandler : IRequestHandler<DefineTimetableCommand>
{
    private readonly IApplicationDbContext _db;

    public DefineTimetableCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DefineTimetableCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.SchoolClasses.AnyAsync(c => c.Id == request.SchoolClassId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("SchoolClass", request.SchoolClassId);
        }

        if (request.SectionId is { } sectionId &&
            !await _db.Sections.AnyAsync(
                    s => s.Id == sectionId && s.SchoolClassId == request.SchoolClassId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("Section (in this class)", sectionId);
        }

        var subjectIds = request.Entries.Select(e => e.SubjectId).Distinct().ToList();
        var known = await _db.Subjects
            .Where(s => subjectIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var missing = subjectIds.Except(known).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException("Subject", missing[0]);
        }

        var existing = await _db.TimetableEntries
            .Where(t => t.SchoolClassId == request.SchoolClassId && t.SectionId == request.SectionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.TimetableEntries.RemoveRange(existing);

        foreach (var entry in request.Entries)
        {
            _db.TimetableEntries.Add(new TimetableEntry
            {
                SchoolClassId = request.SchoolClassId,
                SectionId = request.SectionId,
                DayOfWeek = entry.DayOfWeek,
                Period = entry.Period,
                StartTime = entry.StartTime,
                EndTime = entry.EndTime,
                SubjectId = entry.SubjectId,
                TeacherName = entry.TeacherName?.Trim(),
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Makes the scope's timetable visible to parents.</summary>
public sealed record PublishTimetableCommand(Guid SchoolClassId, Guid? SectionId) : IRequest;

/// <summary>Flips every entry in the scope to published.</summary>
public sealed class PublishTimetableCommandHandler : IRequestHandler<PublishTimetableCommand>
{
    private readonly IApplicationDbContext _db;

    public PublishTimetableCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(PublishTimetableCommand request, CancellationToken cancellationToken)
    {
        var entries = await _db.TimetableEntries
            .Where(t => t.SchoolClassId == request.SchoolClassId && t.SectionId == request.SectionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (entries.Count == 0)
        {
            throw new NotFoundException("Timetable (no entries to publish)", request.SchoolClassId);
        }

        foreach (var entry in entries)
        {
            entry.IsPublished = true;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>The staff view of a scope's timetable (drafts included).</summary>
public sealed record GetTimetableQuery(Guid SchoolClassId, Guid? SectionId)
    : IRequest<IReadOnlyList<TimetableEntryDto>>;

/// <summary>Ordered by day then period.</summary>
public sealed class GetTimetableQueryHandler
    : IRequestHandler<GetTimetableQuery, IReadOnlyList<TimetableEntryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetTimetableQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TimetableEntryDto>> Handle(
        GetTimetableQuery request, CancellationToken cancellationToken) =>
        await _db.TimetableEntries.AsNoTracking()
            .Where(t => t.SchoolClassId == request.SchoolClassId && t.SectionId == request.SectionId)
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.Period)
            .Select(t => new TimetableEntryDto(
                t.Id, t.DayOfWeek, t.Period, t.StartTime, t.EndTime,
                t.SubjectId, t.Subject!.Name, t.TeacherName, t.IsPublished))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>
/// A student's published schedule: class-wide entries plus those targeted at
/// their section, current year's placement.
/// </summary>
public sealed record GetStudentTimetableQuery(Guid StudentId)
    : IRequest<IReadOnlyList<TimetableEntryDto>>;

/// <summary>Resolves placement, then filters to published entries.</summary>
public sealed class GetStudentTimetableQueryHandler
    : IRequestHandler<GetStudentTimetableQuery, IReadOnlyList<TimetableEntryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetStudentTimetableQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TimetableEntryDto>> Handle(
        GetStudentTimetableQuery request, CancellationToken cancellationToken)
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

        return await _db.TimetableEntries.AsNoTracking()
            .Where(t => t.IsPublished &&
                        t.SchoolClassId == placement.SchoolClassId &&
                        (t.SectionId == null || t.SectionId == placement.SectionId))
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.Period)
            .Select(t => new TimetableEntryDto(
                t.Id, t.DayOfWeek, t.Period, t.StartTime, t.EndTime,
                t.SubjectId, t.Subject!.Name, t.TeacherName, t.IsPublished))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
