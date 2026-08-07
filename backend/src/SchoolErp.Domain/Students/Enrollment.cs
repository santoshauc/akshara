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

    public int? RollNumber { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
}
