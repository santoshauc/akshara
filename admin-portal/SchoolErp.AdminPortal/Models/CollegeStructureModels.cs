using SchoolErp.Domain.Academics;

namespace SchoolErp.AdminPortal.Models;

/// <summary>A programme as the register lists it.</summary>
public sealed record ProgrammeDto(
    Guid Id,
    Guid DepartmentId,
    string Name,
    string Code,
    ProgrammeLevel Level,
    int DurationYears,
    int TermsPerYear,
    bool IsActive,
    int Cohorts,
    int Students);

/// <summary>A department with the programmes it runs.</summary>
public sealed record DepartmentDto(
    Guid Id,
    string Name,
    string Code,
    Guid? HeadTeacherId,
    string? HeadTeacherName,
    bool IsActive,
    List<ProgrammeDto> Programmes);

public sealed record CreateDepartmentRequest(string Name, string Code, Guid? HeadTeacherId);

public sealed record UpdateDepartmentRequest(
    string Name, string Code, Guid? HeadTeacherId, bool IsActive);

public sealed record CreateProgrammeRequest(
    Guid DepartmentId,
    string Name,
    string Code,
    ProgrammeLevel Level,
    int DurationYears,
    int TermsPerYear);

public sealed record UpdateProgrammeRequest(
    Guid DepartmentId,
    string Name,
    string Code,
    ProgrammeLevel Level,
    int DurationYears,
    int TermsPerYear,
    bool IsActive);
