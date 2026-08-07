using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Academics;

/// <summary>
/// An academic session of a school (e.g. "2026-27"). Tenant-scoped: every other
/// academic aggregate (classes, enrolment, exams, fees) hangs off a year.
/// </summary>
public class AcademicYear : TenantEntity
{
    /// <summary>Display name, unique within the tenant (e.g. "2026-27").</summary>
    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    /// <summary>
    /// The session currently in progress. At most one per tenant is expected;
    /// enforced at the application layer because promotion briefly overlaps years.
    /// </summary>
    public bool IsCurrent { get; set; }
}
