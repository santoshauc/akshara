using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Students;

public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3,
}

public enum StudentStatus
{
    Active = 1,
    Suspended = 2,
    Withdrawn = 3,
    Alumni = 4,
}

/// <summary>
/// A student of a school. Year-wise class placement lives in
/// <see cref="Enrollment"/>; guardians are linked via <see cref="StudentGuardian"/>.
/// </summary>
public class Student : TenantEntity
{
    /// <summary>School-issued admission number, unique within the tenant.</summary>
    public string AdmissionNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public Gender Gender { get; set; }

    public string? BloodGroup { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? AddressLine1 { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string? PhotoUrl { get; set; }

    /// <summary>Allergies, conditions — surfaced to staff, never to other parents.</summary>
    public string? MedicalNotes { get; set; }

    public DateOnly AdmissionDate { get; set; }

    public StudentStatus Status { get; set; } = StudentStatus.Active;

    public string FullName => $"{FirstName} {LastName}".Trim();

    public ICollection<StudentGuardian> Guardians { get; set; } = [];

    public ICollection<Enrollment> Enrollments { get; set; } = [];
}
