using SchoolErp.Domain.Common;
using SchoolErp.Domain.Staff;

namespace SchoolErp.Domain.Timetable;

/// <summary>
/// A one-day cover arrangement: on <see cref="Date"/>, the slot's regular
/// teacher is replaced by <see cref="SubstituteTeacherId"/>. The base
/// timetable stays untouched — substitutions are date-specific overlays.
/// </summary>
public class TimetableSubstitution : TenantEntity
{
    public DateOnly Date { get; set; }

    public Guid TimetableEntryId { get; set; }

    public TimetableEntry? TimetableEntry { get; set; }

    public Guid AbsentTeacherId { get; set; }

    public Guid SubstituteTeacherId { get; set; }

    public Teacher? SubstituteTeacher { get; set; }
}
