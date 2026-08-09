using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Leave;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Staff;

/// <summary>One slot in the teacher's own day, with the jobs attached to it.</summary>
public sealed record TeacherPeriodDto(
    int Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string SubjectName,
    string ClassName,
    string? SectionName,
    bool IsSubstitution,
    bool AttendanceMarked);

/// <summary>An exam paper of theirs with no marks entered yet.</summary>
public sealed record TeacherMarksTaskDto(
    string ExamName, string SubjectName, string ClassName, DateOnly ExamStartDate);

/// <summary>Homework they set for a class they teach, due today or soon.</summary>
public sealed record TeacherHomeworkTaskDto(
    string Title, string SubjectName, string ClassName, string? SectionName, DateOnly DueDate);

/// <summary>
/// The signed-in teacher's working day: what they teach, what is unmarked,
/// and what is waiting on them. Everything is narrowed to their own slots —
/// a teacher never sees another teacher's classes through this.
/// </summary>
public sealed record MyTeacherDayDto(
    string TeacherName,
    string EmployeeCode,
    DateOnly Date,
    IReadOnlyList<TeacherPeriodDto> Periods,
    IReadOnlyList<TeacherMarksTaskDto> MarksBacklog,
    IReadOnlyList<TeacherHomeworkTaskDto> HomeworkDue,
    int SectionsAwaitingAttendance,
    int PendingLeaveForMyStudents,
    int StudentsTaught);

/// <summary>Today's board for whoever is signed in, if they are a teacher.</summary>
public sealed record GetMyTeacherDayQuery : IRequest<MyTeacherDayDto>;

/// <summary>
/// Resolves the caller's <see cref="Domain.Staff.Teacher"/> by their user id,
/// then builds the day from their timetable slots only. Substitutions overlay
/// the base timetable: periods they cover today are added, periods someone
/// else covers for them are dropped.
/// </summary>
public sealed class GetMyTeacherDayQueryHandler
    : IRequestHandler<GetMyTeacherDayQuery, MyTeacherDayDto>
{
    private const int HomeworkHorizonDays = 7;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public GetMyTeacherDayQueryHandler(
        IApplicationDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<MyTeacherDayDto> Handle(
        GetMyTeacherDayQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            throw new NotFoundException("Teacher", _currentUser.UserId ?? "(anonymous)");
        }

        var me = await _db.Teachers.AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => new { t.Id, t.FullName, t.EmployeeCode })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Teacher", userId);

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var isoDay = (int)today.DayOfWeek == 0 ? 7 : (int)today.DayOfWeek;

        // Base slots for today, plus who is covering what.
        var mySlots = await _db.TimetableEntries.AsNoTracking()
            .Where(e => e.TeacherId == me.Id && e.DayOfWeek == isoDay)
            .Select(e => new SlotRow(
                e.Id, e.Period, e.StartTime, e.EndTime, e.Subject!.Name,
                e.SchoolClassId, e.SectionId, false))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var coveredForMe = await _db.TimetableSubstitutions.AsNoTracking()
            .Where(s => s.Date == today && s.AbsentTeacherId == me.Id)
            .Select(s => s.TimetableEntryId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var coveringToday = await _db.TimetableSubstitutions.AsNoTracking()
            .Where(s => s.Date == today && s.SubstituteTeacherId == me.Id)
            .Select(s => new SlotRow(
                s.TimetableEntry!.Id, s.TimetableEntry.Period,
                s.TimetableEntry.StartTime, s.TimetableEntry.EndTime,
                s.TimetableEntry.Subject!.Name,
                s.TimetableEntry.SchoolClassId, s.TimetableEntry.SectionId, true))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var slots = mySlots
            .Where(s => !coveredForMe.Contains(s.EntryId))
            .Concat(coveringToday)
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.Period)
            .ToList();

        // Names cover the WHOLE school, not just today's slots: the backlog and
        // homework lists reach classes this teacher only meets on other days.
        var classNames = await _db.SchoolClasses.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken)
            .ConfigureAwait(false);
        var sections = await _db.Sections.AsNoTracking()
            .Select(s => new SectionRow(s.Id, s.SchoolClassId, s.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var sectionNames = sections.ToDictionary(s => s.Id, s => s.Name);

        // A class-wide slot (SectionId null) covers every section of the class.
        var todaySectionIds = slots
            .SelectMany(s => SlotSectionIds(s, sections))
            .ToHashSet();

        // Every slot of the week, so the roll and leave counts describe the
        // teacher's whole load rather than whatever today happens to hold.
        var allMySectionIds = (await _db.TimetableEntries.AsNoTracking()
                .Where(e => e.TeacherId == me.Id)
                .Select(e => new { e.SchoolClassId, e.SectionId })
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SelectMany(e => e.SectionId is { } sectionId
                ? [sectionId]
                : sections.Where(x => x.SchoolClassId == e.SchoolClassId).Select(x => x.Id))
            .ToHashSet();

        // Daily roll-call only — period marking is optional and not chased here.
        var markedToday = (await _db.AttendanceRecords.AsNoTracking()
                .Where(a => a.Date == today && a.Period == null && todaySectionIds.Contains(a.SectionId))
                .Select(a => a.SectionId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet();

        var periods = slots
            .Select(s => new TeacherPeriodDto(
                s.Period,
                s.StartTime,
                s.EndTime,
                s.SubjectName,
                classNames.GetValueOrDefault(s.SchoolClassId, "—"),
                s.SectionId is { } sectionId ? sectionNames.GetValueOrDefault(sectionId) : null,
                s.IsSubstitution,
                SlotSectionIds(s, sections).All(markedToday.Contains)))
            .ToList();

        var currentYearId = await _db.AcademicYears.AsNoTracking()
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Every (subject, class) pair they teach in the whole week — the
        // backlog and homework lists are not limited to today's periods.
        var taughtPairs = await _db.TimetableEntries.AsNoTracking()
            .Where(e => e.TeacherId == me.Id)
            .Select(e => new { e.SubjectId, e.SchoolClassId })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var taughtKeys = taughtPairs.Select(p => (p.SubjectId, p.SchoolClassId)).ToHashSet();
        var taughtClassIds = taughtPairs.Select(p => p.SchoolClassId).ToHashSet();

        var marksBacklog = currentYearId is { } backlogYearId
            ? (await (
                    from paper in _db.ExamSubjects.AsNoTracking()
                    join exam in _db.Exams.AsNoTracking() on paper.ExamId equals exam.Id
                    where exam.AcademicYearId == backlogYearId &&
                          !_db.MarkEntries.Any(m => m.ExamSubjectId == paper.Id)
                    select new
                    {
                        exam.Name,
                        exam.StartDate,
                        SubjectName = paper.Subject!.Name,
                        paper.SubjectId,
                        paper.SchoolClassId,
                    })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Where(p => taughtKeys.Contains((p.SubjectId, p.SchoolClassId)))
            .OrderBy(p => p.StartDate)
            .Select(p => new TeacherMarksTaskDto(
                p.Name, p.SubjectName, classNames.GetValueOrDefault(p.SchoolClassId, "—"),
                p.StartDate))
            .ToList()
            : [];

        var horizon = today.AddDays(HomeworkHorizonDays);
        var homeworkDue = (await _db.HomeworkAssignments.AsNoTracking()
                .Where(h => h.DueDate >= today && h.DueDate <= horizon &&
                            taughtClassIds.Contains(h.SchoolClassId))
                .Select(h => new
                {
                    h.Title,
                    SubjectName = h.Subject!.Name,
                    h.SubjectId,
                    h.SchoolClassId,
                    h.SectionId,
                    h.DueDate,
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Where(h => taughtKeys.Contains((h.SubjectId, h.SchoolClassId)))
            .OrderBy(h => h.DueDate)
            .Select(h => new TeacherHomeworkTaskDto(
                h.Title,
                h.SubjectName,
                classNames.GetValueOrDefault(h.SchoolClassId, "—"),
                h.SectionId is { } sectionId ? sectionNames.GetValueOrDefault(sectionId) : null,
                h.DueDate))
            .ToList();

        var myStudentIds = currentYearId is { } rollYearId
            ? await _db.Enrollments.AsNoTracking()
                .Where(e => e.AcademicYearId == rollYearId &&
                            e.Status == EnrollmentStatus.Active &&
                            allMySectionIds.Contains(e.SectionId))
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : [];

        var pendingLeave = myStudentIds.Count == 0
            ? 0
            : await _db.LeaveRequests.AsNoTracking()
                .CountAsync(l => l.Status == LeaveRequestStatus.Pending &&
                                 l.StudentId != null &&
                                 myStudentIds.Contains(l.StudentId.Value), cancellationToken)
                .ConfigureAwait(false);

        return new MyTeacherDayDto(
            me.FullName,
            me.EmployeeCode,
            today,
            periods,
            marksBacklog,
            homeworkDue,
            todaySectionIds.Count(id => !markedToday.Contains(id)),
            pendingLeave,
            myStudentIds.Count);
    }

    /// <summary>Sections a slot actually covers (class-wide slots fan out).</summary>
    private static IEnumerable<Guid> SlotSectionIds(
        SlotRow slot, IReadOnlyCollection<SectionRow> sections) =>
        slot.SectionId is { } sectionId
            ? [sectionId]
            : sections
                .Where(s => s.SchoolClassId == slot.SchoolClassId)
                .Select(s => s.Id);

    /// <summary>Section id with its class, for fanning out class-wide slots.</summary>
    private sealed record SectionRow(Guid Id, Guid SchoolClassId, string Name);

    /// <summary>Flattened timetable slot, base or substitution.</summary>
    private sealed record SlotRow(
        Guid EntryId,
        int Period,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string SubjectName,
        Guid SchoolClassId,
        Guid? SectionId,
        bool IsSubstitution);
}
