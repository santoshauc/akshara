namespace SchoolErp.AdminPortal.Models;

/// <summary>Notice (mirrors NoticeDto).</summary>
public sealed record NoticeDto(
    Guid Id,
    string Title,
    string Body,
    Guid? SchoolClassId,
    string? ClassName,
    DateOnly? ExpiresOn,
    bool IsPinned,
    DateTimeOffset PublishedAt);

/// <summary>Create-notice payload (mirrors CreateNoticeCommand).</summary>
public sealed record CreateNoticeRequest(
    string Title, string Body, Guid? SchoolClassId, DateOnly? ExpiresOn, bool IsPinned);

/// <summary>Homework (mirrors HomeworkDto).</summary>
public sealed record HomeworkDto(
    Guid Id,
    string ClassName,
    string? SectionName,
    string SubjectName,
    string Title,
    string Instructions,
    DateOnly AssignedOn,
    DateOnly DueDate);

/// <summary>Create-homework payload (mirrors CreateHomeworkCommand).</summary>
public sealed record CreateHomeworkRequest(
    Guid SchoolClassId,
    Guid? SectionId,
    Guid SubjectId,
    string Title,
    string Instructions,
    DateOnly DueDate);
