using SchoolErp.Domain.Students;

namespace SchoolErp.AdminPortal.Models;

/// <summary>Academic session as returned by the API.</summary>
public sealed record AcademicYearDto(
    Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, bool IsCurrent);

/// <summary>Section as returned by the API.</summary>
public sealed record SectionDto(Guid Id, string Name, int? Capacity);

/// <summary>Class with sections as returned by the API.</summary>
public sealed record SchoolClassDto(
    Guid Id, string Name, int DisplayOrder, List<SectionDto> Sections);

/// <summary>Create-year payload (mirrors CreateAcademicYearCommand).</summary>
public sealed record CreateAcademicYearRequest(
    string Name, DateOnly StartDate, DateOnly EndDate, bool MakeCurrent);

/// <summary>Create-class payload (mirrors CreateClassCommand).</summary>
public sealed record CreateClassRequest(string Name, int DisplayOrder, List<string> Sections);

/// <summary>Year-end promotion payload (mirrors PromoteClassCommand).</summary>
public sealed record PromoteClassRequest(
    Guid FromAcademicYearId,
    Guid FromClassId,
    Guid FromSectionId,
    Guid ToAcademicYearId,
    Guid ToClassId,
    Guid ToSectionId,
    List<Guid> ExcludedStudentIds);

/// <summary>Outcome of a promotion run.</summary>
public sealed record PromotionResultDto(int Promoted, int Excluded, int AlreadyEnrolled);

/// <summary>Student list row.</summary>
public sealed record StudentListItemDto(
    Guid Id,
    string AdmissionNumber,
    string FirstName,
    string LastName,
    Gender Gender,
    StudentStatus Status,
    string? ClassName,
    string? SectionName,
    int? RollNumber);

/// <summary>Guardian as returned by the API.</summary>
public sealed record GuardianDto(
    Guid Id,
    string FirstName,
    string LastName,
    GuardianRelation Relation,
    string Phone,
    string? Email,
    string? Occupation,
    bool IsPrimary);

/// <summary>Placement as returned by the API.</summary>
public sealed record EnrollmentDto(
    Guid Id,
    Guid AcademicYearId,
    string AcademicYearName,
    Guid SchoolClassId,
    string ClassName,
    Guid SectionId,
    string SectionName,
    int? RollNumber);

/// <summary>Full student detail.</summary>
public sealed record StudentDetailDto(
    Guid Id,
    string AdmissionNumber,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? BloodGroup,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? MedicalNotes,
    DateOnly AdmissionDate,
    StudentStatus Status,
    List<GuardianDto> Guardians,
    EnrollmentDto? CurrentEnrollment,
    string? PhotoUrl);

/// <summary>Guardian input inside an admission (mirrors GuardianInput).</summary>
public sealed record GuardianInputModel
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public GuardianRelation Relation { get; set; } = GuardianRelation.Father;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Occupation { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>Admission payload (mirrors AdmitStudentCommand).</summary>
public sealed record AdmitStudentRequest(
    string? AdmissionNumber,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? BloodGroup,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? MedicalNotes,
    DateOnly AdmissionDate,
    Guid AcademicYearId,
    Guid SchoolClassId,
    Guid SectionId,
    int? RollNumber,
    List<GuardianInputModel> Guardians);

/// <summary>One rejected import row and why.</summary>
public sealed record ImportRowError(int RowNumber, string Message);

/// <summary>Bulk-import outcome: everything landed or nothing did.</summary>
public sealed record ImportStudentsResultDto(
    int TotalRows,
    int Imported,
    List<ImportRowError> Errors);
