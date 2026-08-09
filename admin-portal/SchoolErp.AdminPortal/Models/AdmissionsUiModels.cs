using SchoolErp.Domain.Admissions;

namespace SchoolErp.AdminPortal.Models;

/// <summary>One row on the admissions pipeline board.</summary>
public sealed record EnquiryDto(
    Guid Id,
    string ChildName,
    DateOnly? DateOfBirth,
    string AppliedClass,
    string ParentName,
    string Phone,
    string? Email,
    EnquirySource Source,
    EnquiryStatus Status,
    DateOnly? FollowUpOn,
    bool FollowUpDue,
    string? Notes,
    Guid? StudentId,
    DateTimeOffset CreatedAt);

/// <summary>New-enquiry payload mirroring CreateEnquiryCommand.</summary>
public sealed record CreateEnquiryRequest(
    string ChildName,
    DateOnly? DateOfBirth,
    string AppliedClass,
    string ParentName,
    string Phone,
    string? Email,
    EnquirySource Source,
    DateOnly? FollowUpOn,
    string? Notes);
