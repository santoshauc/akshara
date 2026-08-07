using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Attendance;

public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    Late = 3,
    HalfDay = 4,
    /// <summary>Approved leave — absent but excused.</summary>
    Leave = 5,
}

/// <summary>
/// One student's attendance for one day. Keyed by enrollment (the year-wise
/// placement), with student and section denormalized for the two hot queries:
/// section-by-date (marking grid) and student-by-month (parent calendar).
/// </summary>
public class AttendanceRecord : TenantEntity
{
    public Guid EnrollmentId { get; set; }

    public Guid StudentId { get; set; }

    public Guid SectionId { get; set; }

    public DateOnly Date { get; set; }

    public AttendanceStatus Status { get; set; }

    public string? Remarks { get; set; }
}
