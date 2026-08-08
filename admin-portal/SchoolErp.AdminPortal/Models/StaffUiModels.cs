namespace SchoolErp.AdminPortal.Models;

/// <summary>Teacher row (mirrors TeacherDto).</summary>
public sealed record TeacherDto(
    Guid Id,
    string EmployeeCode,
    string FullName,
    string Phone,
    string? Email,
    string? Qualification,
    string? Specialization,
    DateOnly? JoinedOn,
    bool IsActive,
    bool HasLogin);

/// <summary>Create payload (mirrors CreateTeacherCommand).</summary>
public sealed record CreateTeacherRequest(
    string EmployeeCode,
    string FullName,
    string Phone,
    string? Email,
    string? Qualification,
    string? Specialization,
    DateOnly? JoinedOn);

/// <summary>Update payload (mirrors UpdateTeacherCommand; TeacherId from route).</summary>
public sealed record UpdateTeacherRequest(
    Guid TeacherId,
    string FullName,
    string Phone,
    string? Email,
    string? Qualification,
    string? Specialization,
    DateOnly? JoinedOn,
    bool IsActive);

/// <summary>One schedule slot (mirrors TeacherScheduleItemDto).</summary>
public sealed record TeacherScheduleItemDto(
    int DayOfWeek,
    int Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string SubjectName,
    string ClassName,
    string? SectionName,
    bool IsPublished);
