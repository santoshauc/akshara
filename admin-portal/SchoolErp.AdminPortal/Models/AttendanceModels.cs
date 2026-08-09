using SchoolErp.Domain.Attendance;

namespace SchoolErp.AdminPortal.Models;

/// <summary>One row of the marking grid (mirrors RosterEntryDto).</summary>
public sealed record RosterEntryDto(
    Guid EnrollmentId,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    int? RollNumber,
    AttendanceStatus? Status,
    string? Remarks);

/// <summary>The grid for one section and date (mirrors SectionAttendanceDto).</summary>
public sealed record SectionAttendanceDto(
    Guid SectionId,
    DateOnly Date,
    bool IsMarked,
    List<RosterEntryDto> Roster);

/// <summary>One student's status in a submission (mirrors AttendanceEntry).</summary>
public sealed record AttendanceEntryModel(Guid EnrollmentId, AttendanceStatus Status, string? Remarks);

/// <summary>Marking payload (mirrors MarkAttendanceRequest).</summary>
public sealed record MarkAttendanceRequest(
    DateOnly Date, List<AttendanceEntryModel> Entries, int? Period = null);

/// <summary>One marked day in the month view (mirrors AttendanceDayDto).</summary>
public sealed record AttendanceDayDto(DateOnly Date, AttendanceStatus Status, string? Remarks);

/// <summary>Month calendar summary (mirrors StudentMonthAttendanceDto).</summary>
public sealed record StudentMonthAttendanceDto(
    Guid StudentId,
    int Year,
    int Month,
    List<AttendanceDayDto> Days,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int HalfDayCount,
    int LeaveCount,
    int MarkedDays,
    double AttendancePercent);
