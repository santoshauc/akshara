using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Staff;

/// <summary>
/// A teaching staff member. Timetable slots link here so a teacher's weekly
/// schedule can be derived and double-booking rejected at define time.
/// </summary>
public class Teacher : TenantEntity
{
    /// <summary>School-issued staff code, unique within the tenant (e.g. "EMP-014").</summary>
    public string EmployeeCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>E.164; used for staff contact, unique within the tenant.</summary>
    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>Highest qualification, free text (e.g. "M.Sc., B.Ed.").</summary>
    public string? Qualification { get; set; }

    /// <summary>Subjects this teacher usually handles, free text for now.</summary>
    public string? Specialization { get; set; }

    public DateOnly? JoinedOn { get; set; }

    /// <summary>Inactive teachers stay for history but can't take new periods.</summary>
    public bool IsActive { get; set; } = true;
}
