using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Leave;

public enum LeaveRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

/// <summary>Who the leave is for.</summary>
public enum LeaveApplicantKind
{
    /// <summary>Submitted by a parent for their child.</summary>
    Student = 1,
    /// <summary>Submitted by a staff member for themselves.</summary>
    Staff = 2,
}

/// <summary>
/// A leave request awaiting a staff decision. Student requests come from the
/// parent app; approving one marks the range as Leave in attendance. Staff
/// requests follow the same flow minus the attendance write (staff attendance
/// is not tracked).
/// </summary>
public class LeaveRequest : TenantEntity
{
    public LeaveApplicantKind Kind { get; set; }

    /// <summary>Set for student requests.</summary>
    public Guid? StudentId { get; set; }

    /// <summary>Set for staff requests when the user is a linked teacher.</summary>
    public Guid? TeacherId { get; set; }

    /// <summary>Account that submitted the request (parent or staff member).</summary>
    public Guid RequestedByUserId { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;

    public Guid? DecidedByUserId { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>Optional note shown back to the requester.</summary>
    public string? DecisionNote { get; set; }
}
