using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Timetable;

/// <summary>
/// One period slot of a class timetable. Section-null entries apply to every
/// section of the class. Entries start unpublished; parents only ever see
/// published ones, so staff can redraft safely.
/// </summary>
public class TimetableEntry : TenantEntity
{
    public Guid SchoolClassId { get; set; }

    /// <summary>Null = all sections of the class.</summary>
    public Guid? SectionId { get; set; }

    /// <summary>ISO day: 1 = Monday … 7 = Sunday.</summary>
    public int DayOfWeek { get; set; }

    /// <summary>1-based period number within the day.</summary>
    public int Period { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public Guid SubjectId { get; set; }

    public Subject? Subject { get; set; }

    public string? TeacherName { get; set; }

    /// <summary>Set by the publish command; drafts are staff-only.</summary>
    public bool IsPublished { get; set; }
}
