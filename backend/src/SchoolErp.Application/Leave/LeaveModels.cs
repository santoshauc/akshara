using SchoolErp.Domain.Leave;

namespace SchoolErp.Application.Leave;

/// <summary>A leave request as listed to staff and requesters.</summary>
public sealed record LeaveRequestDto(
    Guid Id,
    LeaveApplicantKind Kind,
    Guid? StudentId,
    string ApplicantName,
    string? ClassName,
    DateOnly FromDate,
    DateOnly ToDate,
    string Reason,
    LeaveRequestStatus Status,
    string? DecisionNote,
    DateTimeOffset RequestedAt);
