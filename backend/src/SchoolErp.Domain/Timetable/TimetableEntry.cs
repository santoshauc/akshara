using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Common;
using SchoolErp.Domain.Staff;

namespace SchoolErp.Domain.Timetable;

/// <summary>
/// What a slot in the day actually is. Indian school days are lessons broken
/// up by a short recess and a lunch break, and those are part of the timetable
/// a parent reads — but they are not taught, not numbered and not attended.
/// </summary>
public enum TimetableSlotKind
{
    /// <summary>A taught period. Carries a subject and a period number.</summary>
    Lesson = 1,

    /// <summary>Short recess / tiffin break between periods.</summary>
    Break = 2,

    /// <summary>The lunch break.</summary>
    Lunch = 3,
}

/// <summary>
/// One slot of a class timetable. Section-null entries apply to every section
/// of the class. Entries start unpublished; parents only ever see published
/// ones, so staff can redraft safely.
/// <para>
/// Break and lunch slots sit in the same table because they are part of the
/// same timetable, but they carry no <see cref="SubjectId"/>, no teacher and
/// no <see cref="Period"/>. Leaving the period number off is deliberate: the
/// alternative — giving recess a number — would renumber every lesson after
/// it, and period-wise attendance already stores those numbers.
/// </para>
/// </summary>
public class TimetableEntry : TenantEntity
{
    public Guid SchoolClassId { get; set; }

    /// <summary>Null = all sections of the class.</summary>
    public Guid? SectionId { get; set; }

    /// <summary>ISO day: 1 = Monday … 7 = Sunday.</summary>
    public int DayOfWeek { get; set; }

    /// <summary>Lesson, recess or lunch.</summary>
    public TimetableSlotKind SlotKind { get; set; } = TimetableSlotKind.Lesson;

    /// <summary>
    /// 1-based period number within the day; null for breaks, which are not
    /// numbered. Slots are ordered by <see cref="StartTime"/>, not by this.
    /// </summary>
    public int? Period { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    /// <summary>What the school calls this break ("Tiffin break"); null for lessons.</summary>
    public string? Label { get; set; }

    /// <summary>The taught subject; null for breaks.</summary>
    public Guid? SubjectId { get; set; }

    public Subject? Subject { get; set; }

    /// <summary>Linked staff member; null for unassigned or guest slots, and for breaks.</summary>
    public Guid? TeacherId { get; set; }

    public Teacher? Teacher { get; set; }

    /// <summary>Free-text fallback when no staff record is linked (guest teachers).</summary>
    public string? TeacherName { get; set; }

    /// <summary>Set by the publish command; drafts are staff-only.</summary>
    public bool IsPublished { get; set; }

    /// <summary>True for anything that is not a taught period.</summary>
    public bool IsBreak => SlotKind != TimetableSlotKind.Lesson;
}
