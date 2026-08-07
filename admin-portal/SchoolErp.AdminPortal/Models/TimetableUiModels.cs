namespace SchoolErp.AdminPortal.Models;

/// <summary>Timetable slot (mirrors TimetableEntryDto).</summary>
public sealed record TimetableEntryDto(
    Guid Id,
    int DayOfWeek,
    int Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid SubjectId,
    string SubjectName,
    Guid? TeacherId,
    string? TeacherName,
    bool IsPublished);

/// <summary>Slot input (mirrors TimetableEntryInput).</summary>
public sealed record TimetableEntryInput(
    int DayOfWeek,
    int Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid SubjectId,
    Guid? TeacherId,
    string? TeacherName);

/// <summary>Define payload (mirrors DefineTimetableCommand).</summary>
public sealed record DefineTimetableRequest(
    Guid SchoolClassId, Guid? SectionId, List<TimetableEntryInput> Entries);

/// <summary>Publish payload (mirrors PublishTimetableCommand).</summary>
public sealed record PublishTimetableRequest(Guid SchoolClassId, Guid? SectionId);
