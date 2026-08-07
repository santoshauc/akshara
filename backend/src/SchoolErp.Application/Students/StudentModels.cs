using AutoMapper;
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

/// <summary>AutoMapper profile for the students module.</summary>
public sealed class StudentsProfile : Profile
{
    public StudentsProfile()
    {
        CreateMap<Enrollment, EnrollmentDto>()
            .ForMember(d => d.AcademicYearName, o => o.MapFrom(e => e.AcademicYear!.Name))
            .ForMember(d => d.ClassName, o => o.MapFrom(e => e.SchoolClass!.Name))
            .ForMember(d => d.SectionName, o => o.MapFrom(e => e.Section!.Name));
    }
}
