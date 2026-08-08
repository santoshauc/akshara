using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Students;

/// <summary>Guardian projection.</summary>
public sealed record GuardianDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public GuardianRelation Relation { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Occupation { get; init; }
    public bool IsPrimary { get; init; }
}

/// <summary>Row shape for the students list.</summary>
public sealed record StudentListItemDto
{
    public Guid Id { get; init; }
    public string AdmissionNumber { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public Gender Gender { get; init; }
    public StudentStatus Status { get; init; }
    public string? ClassName { get; init; }
    public string? SectionName { get; init; }
    public int? RollNumber { get; init; }
}

/// <summary>Full student detail with guardians and current placement.</summary>
public sealed record StudentDetailDto
{
    public Guid Id { get; init; }
    public string AdmissionNumber { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public DateOnly DateOfBirth { get; init; }
    public Gender Gender { get; init; }
    public string? BloodGroup { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? MedicalNotes { get; init; }
    public DateOnly AdmissionDate { get; init; }
    public StudentStatus Status { get; init; }
    public IReadOnlyList<GuardianDto> Guardians { get; init; } = [];
    public EnrollmentDto? CurrentEnrollment { get; init; }
}

/// <summary>Placement projection.</summary>
public sealed record EnrollmentDto
{
    public Guid Id { get; init; }
    public Guid AcademicYearId { get; init; }
    public string AcademicYearName { get; init; } = string.Empty;
    public Guid SchoolClassId { get; init; }
    public string ClassName { get; init; } = string.Empty;
    public Guid SectionId { get; init; }
    public string SectionName { get; init; } = string.Empty;
    public int? RollNumber { get; init; }
    public EnrollmentStatus Status { get; init; }
}

/// <summary>Guardian input used during admission.</summary>
public sealed record GuardianInput(
    string FirstName,
    string LastName,
    GuardianRelation Relation,
    string Phone,
    string? Email,
    string? Occupation,
    bool IsPrimary);

/// <summary>Hand-written mappings for the students module.</summary>
public static class StudentMappings
{
    /// <summary>Requires AcademicYear/SchoolClass/Section navigations to be loaded.</summary>
    public static EnrollmentDto ToDto(this Enrollment enrollment) => new()
    {
        Id = enrollment.Id,
        AcademicYearId = enrollment.AcademicYearId,
        AcademicYearName = enrollment.AcademicYear?.Name ?? string.Empty,
        SchoolClassId = enrollment.SchoolClassId,
        ClassName = enrollment.SchoolClass?.Name ?? string.Empty,
        SectionId = enrollment.SectionId,
        SectionName = enrollment.Section?.Name ?? string.Empty,
        RollNumber = enrollment.RollNumber,
        Status = enrollment.Status,
    };
}
