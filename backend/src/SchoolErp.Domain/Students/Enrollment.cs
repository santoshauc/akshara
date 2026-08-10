using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Students;

public enum EnrollmentStatus
{
    Active = 1,
    /// <summary>Closed by promotion into the next academic year.</summary>
    Promoted = 2,
    /// <summary>Final year completed.</summary>
    Completed = 3,
    /// <summary>Left mid-year (transfer/withdrawal).</summary>
    Left = 4,
}

/// <summary>
/// A student's placement for one academic year. Promotion never mutates an
/// enrollment — it closes this one and creates the next, preserving history.
/// </summary>
public class Enrollment : TenantEntity
{
    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public AcademicYear? AcademicYear { get; set; }

    public Guid SchoolClassId { get; set; }

    public SchoolClass? SchoolClass { get; set; }

    public Guid SectionId { get; set; }

    public Section? Section { get; set; }

    /// <summary>
    /// The programme this placement was under, at a college. Copied from the
    /// class at admission rather than asked for, and NEVER re-read from the
    /// class afterwards: re-pointing a cohort at a different programme must
    /// not silently rewrite what past students were enrolled in. Null at a
    /// school, and at a college for cohorts created before the programme was.
    /// </summary>
    public Guid? ProgrammeId { get; set; }

    public int? RollNumber { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
}
