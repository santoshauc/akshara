using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Timetable;

namespace SchoolErp.Application.Timetable;

/// <summary>One slot needing cover, with the teachers free to take it.</summary>
public sealed record SubstitutionSlotDto(
    Guid TimetableEntryId,
    int Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string SubjectName,
    string ClassName,
    string? SectionName,
    Guid? AlreadySubstitutedBy,
    IReadOnlyList<FreeTeacherDto> FreeTeachers);

/// <summary>A teacher available for a slot.</summary>
public sealed record FreeTeacherDto(Guid TeacherId, string FullName);

/// <summary>An applied substitution as listed for a date.</summary>
public sealed record SubstitutionDto(
    Guid Id,
    DateOnly Date,
    int Period,
    string SubjectName,
    string ClassName,
    string AbsentTeacherName,
    string SubstituteTeacherName);

/// <summary>
/// The cover plan when a teacher is absent on a date: their published slots
/// for that weekday, each with the active teachers who are free then (not
/// teaching, not already covering another class).
/// </summary>
public sealed record GetSubstitutionPlanQuery(Guid TeacherId, DateOnly Date)
    : IRequest<IReadOnlyList<SubstitutionSlotDto>>;

/// <summary>Computes free teachers per clashing slot.</summary>
public sealed class GetSubstitutionPlanQueryHandler
    : IRequestHandler<GetSubstitutionPlanQuery, IReadOnlyList<SubstitutionSlotDto>>
{
    private readonly IApplicationDbContext _db;

    public GetSubstitutionPlanQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SubstitutionSlotDto>> Handle(
        GetSubstitutionPlanQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.Teachers.AnyAsync(t => t.Id == request.TeacherId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("Teacher", request.TeacherId);
        }

        var isoDay = (int)request.Date.DayOfWeek;
        var day = isoDay == 0 ? 7 : isoDay;

        var absentSlots = await _db.TimetableEntries.AsNoTracking()
            // Only lessons need covering — nobody substitutes for lunch.
            .Where(t => t.TeacherId == request.TeacherId &&
                        t.DayOfWeek == day && t.IsPublished &&
                        t.SlotKind == TimetableSlotKind.Lesson)
            .OrderBy(t => t.StartTime)
            .Select(t => new
            {
                t.Id,
                Period = t.Period!.Value,
                t.StartTime,
                t.EndTime,
                SubjectName = t.Subject!.Name,
                ClassName = _db.SchoolClasses.Where(c => c.Id == t.SchoolClassId)
                    .Select(c => c.Name).First(),
                SectionName = t.SectionId == null
                    ? null
                    : _db.Sections.Where(s => s.Id == t.SectionId)
                        .Select(s => s.Name).First(),
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (absentSlots.Count == 0)
        {
            return [];
        }

        // Everyone else's commitments that weekday, plus covers already
        // applied for this DATE — a teacher busy either way is not free.
        var busy = await _db.TimetableEntries.AsNoTracking()
            .Where(t => t.DayOfWeek == day && t.IsPublished && t.TeacherId != null &&
                        t.TeacherId != request.TeacherId)
            .Select(t => new { TeacherId = t.TeacherId!.Value, t.StartTime, t.EndTime })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var covering = await _db.TimetableSubstitutions.AsNoTracking()
            .Where(s => s.Date == request.Date)
            .Select(s => new
            {
                TeacherId = s.SubstituteTeacherId,
                s.TimetableEntry!.StartTime,
                s.TimetableEntry.EndTime,
                s.TimetableEntryId,
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var candidates = await _db.Teachers.AsNoTracking()
            .Where(t => t.IsActive && t.Id != request.TeacherId)
            .OrderBy(t => t.FullName)
            .Select(t => new FreeTeacherDto(t.Id, t.FullName))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var existingCovers = covering.ToDictionary(c => c.TimetableEntryId, c => c.TeacherId);

        return absentSlots.Select(slot => new SubstitutionSlotDto(
            slot.Id,
            slot.Period,
            slot.StartTime,
            slot.EndTime,
            slot.SubjectName,
            slot.ClassName,
            slot.SectionName,
            existingCovers.TryGetValue(slot.Id, out var coveredBy) ? coveredBy : null,
            candidates
                .Where(c =>
                    !busy.Any(b => b.TeacherId == c.TeacherId &&
                                   b.StartTime < slot.EndTime && slot.StartTime < b.EndTime) &&
                    !covering.Any(v => v.TeacherId == c.TeacherId &&
                                       v.StartTime < slot.EndTime && slot.StartTime < v.EndTime))
                .ToList()))
            .ToList();
    }
}

/// <summary>One slot → substitute assignment.</summary>
public sealed record SubstitutionInput(Guid TimetableEntryId, Guid SubstituteTeacherId);

/// <summary>Publishes the day's cover plan (upserts per slot).</summary>
public sealed record ApplySubstitutionsCommand(
    Guid AbsentTeacherId,
    DateOnly Date,
    IReadOnlyList<SubstitutionInput> Items) : IRequest<int>;

/// <summary>Shape rules.</summary>
public sealed class ApplySubstitutionsCommandValidator
    : AbstractValidator<ApplySubstitutionsCommand>
{
    public ApplySubstitutionsCommandValidator()
    {
        RuleFor(c => c.Items).NotEmpty()
            .Must(i => i.Select(x => x.TimetableEntryId).Distinct().Count() == i.Count)
            .WithMessage("Each slot may appear only once.");
    }
}

/// <summary>Validates each cover and upserts the day's records.</summary>
public sealed class ApplySubstitutionsCommandHandler
    : IRequestHandler<ApplySubstitutionsCommand, int>
{
    private readonly IApplicationDbContext _db;

    public ApplySubstitutionsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<int> Handle(ApplySubstitutionsCommand request, CancellationToken cancellationToken)
    {
        var entryIds = request.Items.Select(i => i.TimetableEntryId).ToList();
        var entries = await _db.TimetableEntries
            .Where(t => entryIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken).ConfigureAwait(false);
        var teacherIds = request.Items.Select(i => i.SubstituteTeacherId).Distinct().ToList();
        var activeTeachers = await _db.Teachers
            .Where(t => teacherIds.Contains(t.Id) && t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var existing = await _db.TimetableSubstitutions
            .Where(s => s.Date == request.Date && entryIds.Contains(s.TimetableEntryId))
            .ToDictionaryAsync(s => s.TimetableEntryId, cancellationToken).ConfigureAwait(false);

        foreach (var item in request.Items)
        {
            if (!entries.TryGetValue(item.TimetableEntryId, out var entry))
            {
                throw new NotFoundException("TimetableEntry", item.TimetableEntryId);
            }

            if (!activeTeachers.Contains(item.SubstituteTeacherId))
            {
                throw new NotFoundException("Teacher (active)", item.SubstituteTeacherId);
            }

            if (entry.TeacherId == item.SubstituteTeacherId)
            {
                throw new ConflictException(
                    "A teacher cannot substitute for their own slot.");
            }

            if (existing.TryGetValue(item.TimetableEntryId, out var substitution))
            {
                substitution.SubstituteTeacherId = item.SubstituteTeacherId;
                substitution.AbsentTeacherId = request.AbsentTeacherId;
            }
            else
            {
                _db.TimetableSubstitutions.Add(new TimetableSubstitution
                {
                    Date = request.Date,
                    TimetableEntryId = item.TimetableEntryId,
                    AbsentTeacherId = request.AbsentTeacherId,
                    SubstituteTeacherId = item.SubstituteTeacherId,
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Items.Count;
    }
}

/// <summary>The cover list for a date (staff room noticeboard view).</summary>
public sealed record GetSubstitutionsQuery(DateOnly Date)
    : IRequest<IReadOnlyList<SubstitutionDto>>;

/// <summary>Projection with names resolved.</summary>
public sealed class GetSubstitutionsQueryHandler
    : IRequestHandler<GetSubstitutionsQuery, IReadOnlyList<SubstitutionDto>>
{
    private readonly IApplicationDbContext _db;

    public GetSubstitutionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SubstitutionDto>> Handle(
        GetSubstitutionsQuery request, CancellationToken cancellationToken) =>
        await _db.TimetableSubstitutions.AsNoTracking()
            .Where(s => s.Date == request.Date)
            .OrderBy(s => s.TimetableEntry!.StartTime)
            .Select(s => new SubstitutionDto(
                s.Id,
                s.Date,
                s.TimetableEntry!.Period!.Value,
                s.TimetableEntry.Subject!.Name,
                _db.SchoolClasses
                    .Where(c => c.Id == s.TimetableEntry.SchoolClassId)
                    .Select(c => c.Name).First(),
                _db.Teachers.Where(t => t.Id == s.AbsentTeacherId)
                    .Select(t => t.FullName).FirstOrDefault() ?? "—",
                s.SubstituteTeacher!.FullName))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}
