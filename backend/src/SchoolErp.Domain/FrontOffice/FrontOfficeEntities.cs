using SchoolErp.Domain.Common;
using SchoolErp.Domain.Students;

namespace SchoolErp.Domain.FrontOffice;

/// <summary>Why someone came to the school gate.</summary>
public enum VisitorPurpose
{
    /// <summary>Parent or guardian meeting staff about a student.</summary>
    ParentMeeting = 1,

    /// <summary>Prospective parent asking about admission.</summary>
    AdmissionEnquiry = 2,

    /// <summary>Courier, supplier or maintenance.</summary>
    Delivery = 3,

    /// <summary>Government or board official.</summary>
    Official = 4,

    Other = 5,
}

/// <summary>
/// One visit to the school, logged at the front desk. Open visits have a null
/// <see cref="CheckedOutAt"/> — the desk can see at a glance who is still on
/// the premises, which is the whole point of a gate register.
/// </summary>
public class VisitorEntry : TenantEntity
{
    public string VisitorName { get; set; } = string.Empty;

    /// <summary>E.164 contact number; the desk asks for it on arrival.</summary>
    public string? Phone { get; set; }

    public VisitorPurpose Purpose { get; set; } = VisitorPurpose.Other;

    /// <summary>Free-text note of who they came to see (staff name, office).</summary>
    public string? WhomToMeet { get; set; }

    /// <summary>Set when the visit concerns a particular student.</summary>
    public Guid? StudentId { get; set; }

    public Student? Student { get; set; }

    /// <summary>Badge handed over at the desk, unique per school per day.</summary>
    public string PassNumber { get; set; } = string.Empty;

    public DateTimeOffset CheckedInAt { get; set; }

    /// <summary>Null while the visitor is still inside.</summary>
    public DateTimeOffset? CheckedOutAt { get; set; }

    public string? Remarks { get; set; }
}

/// <summary>
/// Permission for a student to leave before the school day ends. Issued at the
/// desk, released to a named adult, and closed when the student returns (or
/// left open if they went home for the day).
/// </summary>
public class GatePass : TenantEntity
{
    public Guid StudentId { get; set; }

    public Student? Student { get; set; }

    /// <summary>Sequential per school (GP-2026-0001).</summary>
    public string PassNumber { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    /// <summary>Adult the child was handed to — the accountability record.</summary>
    public string ReleasedTo { get; set; } = string.Empty;

    public string? ReleasedToPhone { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>Set when the student comes back the same day.</summary>
    public DateTimeOffset? ReturnedAt { get; set; }

    /// <summary>Staff account that authorised the release.</summary>
    public Guid? ApprovedByUserId { get; set; }
}
