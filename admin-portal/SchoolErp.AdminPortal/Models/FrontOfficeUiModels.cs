using SchoolErp.Domain.FrontOffice;

namespace SchoolErp.AdminPortal.Models;

/// <summary>Gate register row (mirrors VisitorEntryDto).</summary>
public sealed record VisitorEntryDto(
    Guid Id,
    string VisitorName,
    string? Phone,
    VisitorPurpose Purpose,
    string? WhomToMeet,
    Guid? StudentId,
    string? StudentName,
    string PassNumber,
    DateTimeOffset CheckedInAt,
    DateTimeOffset? CheckedOutAt,
    string? Remarks);

/// <summary>Check-in payload (mirrors CheckInVisitorCommand).</summary>
public sealed record CheckInVisitorRequest(
    string VisitorName,
    string? Phone,
    VisitorPurpose Purpose,
    string? WhomToMeet,
    Guid? StudentId,
    string? Remarks);

/// <summary>Early-release row (mirrors GatePassDto).</summary>
public sealed record GatePassDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string? ClassName,
    string PassNumber,
    string Reason,
    string ReleasedTo,
    string? ReleasedToPhone,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ReturnedAt);

/// <summary>Gate-pass payload (mirrors IssueGatePassCommand).</summary>
public sealed record IssueGatePassRequest(
    Guid StudentId,
    string Reason,
    string ReleasedTo,
    string? ReleasedToPhone);
