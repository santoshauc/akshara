using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Admissions;

/// <summary>Where the enquiry came from.</summary>
public enum EnquirySource
{
    WalkIn = 1,
    Phone = 2,
    Website = 3,
    Referral = 4,
}

/// <summary>The admissions pipeline stage.</summary>
public enum EnquiryStatus
{
    New = 1,
    Contacted = 2,
    /// <summary>School visit / tour scheduled or done.</summary>
    Visit = 3,
    Admitted = 4,
    Lost = 5,
}

/// <summary>
/// A prospective admission: captured at first contact, worked through the
/// pipeline, and (ideally) converted into a real Student — the conversion
/// stamps <see cref="StudentId"/> so the funnel stays measurable.
/// </summary>
public class AdmissionEnquiry : TenantEntity
{
    public string ChildName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Free text — applicants may target classes not configured yet.</summary>
    public string AppliedClass { get; set; } = string.Empty;

    public string ParentName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public EnquirySource Source { get; set; }

    public EnquiryStatus Status { get; set; } = EnquiryStatus.New;

    /// <summary>Next follow-up date; drives the "due today" reminders.</summary>
    public DateOnly? FollowUpOn { get; set; }

    public string? Notes { get; set; }

    /// <summary>The admitted student, once converted.</summary>
    public Guid? StudentId { get; set; }
}
