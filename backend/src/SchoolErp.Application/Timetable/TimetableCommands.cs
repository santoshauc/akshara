using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Timetable;

namespace SchoolErp.Application.Timetable;

/// <summary>
/// One slot as shown in grids. TeacherName resolves from the linked staff
/// record when present, else the free-text fallback. For a break, Period,
/// SubjectId, SubjectName and both teacher fields are null and Label carries
/// what the school calls it.
/// </summary>
public sealed record TimetableEntryDto(
    Guid Id,
    int DayOfWeek,
    int? Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? SubjectId,
    string? SubjectName,
    Guid? TeacherId,
    string? TeacherName,
    bool IsPublished,
    TimetableSlotKind SlotKind = TimetableSlotKind.Lesson,
    string? Label = null);

/// <summary>
/// Input slot when defining a timetable. TeacherId links a staff record;
/// TeacherName is the free-text fallback for guest teachers. A break passes
/// SlotKind plus times (and optionally a Label) and leaves the rest null.
/// </summary>
public sealed record TimetableEntryInput(
    int DayOfWeek,
    int? Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? SubjectId,
    Guid? TeacherId,
    string? TeacherName,
    TimetableSlotKind SlotKind = TimetableSlotKind.Lesson,
    string? Label = null);

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
            // Breaks carry no period number, so uniqueness only binds lessons.
            .Must(entries => entries
                .Where(e => e.SlotKind == TimetableSlotKind.Lesson)
                .Select(e => (e.DayOfWeek, e.Period))
                .Distinct()
                .Count() == entries.Count(e => e.SlotKind == TimetableSlotKind.Lesson))
            .WithMessage("Each (day, period) slot may appear only once.");

        RuleForEach(c => c.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.DayOfWeek).InclusiveBetween(1, 7);
            entry.RuleFor(e => e.EndTime).GreaterThan(e => e.StartTime)
                .WithMessage("A slot must end after it starts.");
            entry.RuleFor(e => e.TeacherName).MaximumLength(128);
            entry.RuleFor(e => e.Label).MaximumLength(50);

            entry.When(e => e.SlotKind == TimetableSlotKind.Lesson, () =>
            {
                entry.RuleFor(e => e.Period).NotNull().InclusiveBetween(1, 12)
                    .WithMessage("A taught period needs a period number between 1 and 12.");
                entry.RuleFor(e => e.SubjectId).NotNull()
                    .WithMessage("A taught period needs a subject.");
            });

            entry.When(e => e.SlotKind != TimetableSlotKind.Lesson, () =>
            {
                // Nobody teaches lunch. Silently dropping these would make the
                // grid disagree with what the operator submitted.
                entry.RuleFor(e => e.SubjectId).Null()
                    .WithMessage("A break cannot have a subject.");
                entry.RuleFor(e => e.TeacherId).Null()
                    .WithMessage("A break cannot have a teacher.");
                entry.RuleFor(e => e.Period).Null()
                    .WithMessage("A break is not a numbered period.");
            });
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

        var subjectIds = request.Entries
            .Where(e => e.SubjectId is not null)
            .Select(e => e.SubjectId!.Value)
            .Distinct()
            .ToList();
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

        await EnsureTeachersFreeAsync(request, cancellationToken).ConfigureAwait(false);

        // AFTER the teacher check on purpose: when a teacher is double-booked
        // that message names them and their periods, which is far more useful
        // than "these two slots overlap". This catches what that cannot — a
        // lunch break laid over a period, where there is no teacher involved.
        EnsureNoOverlaps(request);

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
                SlotKind = entry.SlotKind,
                Period = entry.Period,
                StartTime = entry.StartTime,
                EndTime = entry.EndTime,
                Label = entry.Label?.Trim() is { Length: > 0 } label ? label : null,
                SubjectId = entry.SubjectId,
                TeacherId = entry.TeacherId,
                TeacherName = entry.TeacherName?.Trim(),
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A class cannot be in two places at once, so no two slots of one scope
    /// may overlap on a day. Times order the day now that breaks carry no
    /// period number, so an overlap is not merely untidy — it makes the day
    /// ambiguous.
    /// </summary>
    private static void EnsureNoOverlaps(DefineTimetableCommand request)
    {
        foreach (var day in request.Entries.GroupBy(e => e.DayOfWeek))
        {
            // Sorted by start, any overlap implies an adjacent one.
            var sorted = day.OrderBy(e => e.StartTime).ToList();
            for (var i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].StartTime < sorted[i - 1].EndTime)
                {
                    throw new ConflictException(
                        $"Two slots overlap on day {day.Key}: " +
                        $"{sorted[i - 1].StartTime:HH\\:mm}–{sorted[i - 1].EndTime:HH\\:mm} " +
                        $"and {sorted[i].StartTime:HH\\:mm}–{sorted[i].EndTime:HH\\:mm}.");
                }
            }
        }
    }

    /// <summary>
    /// Rejects the define when any linked teacher is unknown/inactive or would
    /// be double-booked: two slots for the same teacher on the same day with
    /// overlapping times — within this submission or against another class
    /// scope's existing entries (this scope is being replaced, so it's excluded).
    /// </summary>
    private async Task EnsureTeachersFreeAsync(
        DefineTimetableCommand request, CancellationToken cancellationToken)
    {
        var teacherIds = request.Entries
            .Where(e => e.TeacherId is not null)
            .Select(e => e.TeacherId!.Value)
            .Distinct()
            .ToList();
        if (teacherIds.Count == 0)
        {
            return;
        }

        var teachers = await _db.Teachers
            .Where(t => teacherIds.Contains(t.Id))
            .Select(t => new { t.Id, t.FullName, t.IsActive })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var missingTeacher = teacherIds.Except(teachers.Select(t => t.Id)).ToList();
        if (missingTeacher.Count > 0)
        {
            throw new NotFoundException("Teacher", missingTeacher[0]);
        }

        var inactive = teachers.FirstOrDefault(t => !t.IsActive);
        if (inactive is not null)
        {
            throw new ConflictException(
                $"{inactive.FullName} is inactive and cannot be scheduled.");
        }

        var names = teachers.ToDictionary(t => t.Id, t => t.FullName);

        // Clashes inside the submitted batch itself. Sorted by start time,
        // any overlap implies an adjacent overlap, so one pass suffices.
        foreach (var group in request.Entries
                     .Where(e => e.TeacherId is not null)
                     .GroupBy(e => (e.TeacherId, e.DayOfWeek)))
        {
            var sorted = group.OrderBy(e => e.StartTime).ToList();
            for (var i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].StartTime < sorted[i - 1].EndTime)
                {
                    throw new ConflictException(
                        $"{names[group.Key.TeacherId!.Value]} is scheduled twice at " +
                        $"overlapping times (day {group.Key.DayOfWeek}, periods " +
                        $"{sorted[i - 1].Period} and {sorted[i].Period}) in this timetable.");
                }
            }
        }

        // Clashes against other class scopes' existing entries.
        var days = request.Entries.Select(e => e.DayOfWeek).Distinct().ToList();
        var others = await _db.TimetableEntries.AsNoTracking()
            .Where(t => t.TeacherId != null &&
                        teacherIds.Contains(t.TeacherId.Value) &&
                        days.Contains(t.DayOfWeek) &&
                        !(t.SchoolClassId == request.SchoolClassId && t.SectionId == request.SectionId))
            .Select(t => new
            {
                t.TeacherId,
                t.DayOfWeek,
                t.StartTime,
                t.EndTime,
                ClassName = _db.SchoolClasses
                    .Where(c => c.Id == t.SchoolClassId).Select(c => c.Name).First(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var entry in request.Entries.Where(e => e.TeacherId is not null))
        {
            var clash = others.FirstOrDefault(o =>
                o.TeacherId == entry.TeacherId &&
                o.DayOfWeek == entry.DayOfWeek &&
                entry.StartTime < o.EndTime &&
                o.StartTime < entry.EndTime);
            if (clash is not null)
            {
                throw new ConflictException(
                    $"{names[entry.TeacherId!.Value]} already teaches {clash.ClassName} on " +
                    $"day {clash.DayOfWeek} at {clash.StartTime:HH\\:mm}–{clash.EndTime:HH\\:mm}; " +
                    $"period {entry.Period} overlaps.");
            }
        }
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
            // Start time, not period: a break has no number to sort by.
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartTime)
            .Select(t => new TimetableEntryDto(
                t.Id, t.DayOfWeek, t.Period, t.StartTime, t.EndTime,
                t.SubjectId, t.Subject != null ? t.Subject.Name : null,
                t.TeacherId,
                t.Teacher != null ? t.Teacher.FullName : t.TeacherName,
                t.IsPublished, t.SlotKind, t.Label))
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
            // Start time, not period: a break has no number to sort by.
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartTime)
            .Select(t => new TimetableEntryDto(
                t.Id, t.DayOfWeek, t.Period, t.StartTime, t.EndTime,
                t.SubjectId, t.Subject != null ? t.Subject.Name : null,
                t.TeacherId,
                t.Teacher != null ? t.Teacher.FullName : t.TeacherName,
                t.IsPublished, t.SlotKind, t.Label))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
