using SchoolErp.Domain.Timetable;

namespace SchoolErp.AdminPortal.Models;

/// <summary>
/// Timetable slot (mirrors TimetableEntryDto). A break carries no period
/// number, subject or teacher — just times, a kind and an optional label.
/// </summary>
public sealed record TimetableEntryDto(
    Guid Id,
    int DayOfWeek,
    int? Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? SubjectId,
    string? SubjectName,
    Guid? TeacherId,
    string? TeacherName,
    bool IsPublished,
    TimetableSlotKind SlotKind = TimetableSlotKind.Lesson,
    string? Label = null);

/// <summary>Slot input (mirrors TimetableEntryInput).</summary>
public sealed record TimetableEntryInput(
    int DayOfWeek,
    int? Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? SubjectId,
    Guid? TeacherId,
    string? TeacherName,
    TimetableSlotKind SlotKind = TimetableSlotKind.Lesson,
    string? Label = null);

/// <summary>Define payload (mirrors DefineTimetableCommand).</summary>
public sealed record DefineTimetableRequest(
    Guid SchoolClassId, Guid? SectionId, List<TimetableEntryInput> Entries);

/// <summary>Publish payload (mirrors PublishTimetableCommand).</summary>
public sealed record PublishTimetableRequest(Guid SchoolClassId, Guid? SectionId);

/// <summary>Cover-plan slot (mirrors SubstitutionSlotDto).</summary>
public sealed record SubstitutionSlotDto(
    Guid TimetableEntryId,
    int Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string SubjectName,
    string ClassName,
    string? SectionName,
    Guid? AlreadySubstitutedBy,
    List<FreeTeacherDto> FreeTeachers);

/// <summary>Free teacher option (mirrors FreeTeacherDto).</summary>
public sealed record FreeTeacherDto(Guid TeacherId, string FullName);

/// <summary>Applied cover row (mirrors SubstitutionDto).</summary>
public sealed record SubstitutionDto(
    Guid Id,
    DateOnly Date,
    int Period,
    string SubjectName,
    string ClassName,
    string AbsentTeacherName,
    string SubstituteTeacherName);

/// <summary>Cover input line (mirrors SubstitutionInput).</summary>
public sealed record SubstitutionInput(Guid TimetableEntryId, Guid SubstituteTeacherId);
