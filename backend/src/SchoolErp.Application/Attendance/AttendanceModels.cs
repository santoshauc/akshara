using SchoolErp.Domain.Attendance;

namespace SchoolErp.Application.Attendance;

/// <summary>One row of the section marking grid.</summary>
public sealed record RosterEntryDto
{
    public Guid EnrollmentId { get; init; }
    public Guid StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string AdmissionNumber { get; init; } = string.Empty;
    public int? RollNumber { get; init; }
    /// <summary>Null when not yet marked for the day.</summary>
    public AttendanceStatus? Status { get; init; }
    public string? Remarks { get; init; }
}

/// <summary>The marking grid for one section and date.</summary>
public sealed record SectionAttendanceDto
{
    public Guid SectionId { get; init; }
    public DateOnly Date { get; init; }
    public bool IsMarked { get; init; }
    public IReadOnlyList<RosterEntryDto> Roster { get; init; } = [];
}

/// <summary>One marked day in a student's month view.</summary>
public sealed record AttendanceDayDto(DateOnly Date, AttendanceStatus Status, string? Remarks);

/// <summary>A student's attendance for one month, with counters for the calendar UI.</summary>
public sealed record StudentMonthAttendanceDto
{
    public Guid StudentId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public IReadOnlyList<AttendanceDayDto> Days { get; init; } = [];
    public int PresentCount { get; init; }
    public int AbsentCount { get; init; }
    public int LateCount { get; init; }
    public int HalfDayCount { get; init; }
    public int LeaveCount { get; init; }
    public int MarkedDays { get; init; }
    /// <summary>Present + Late + HalfDay as a share of marked days (0–100).</summary>
    public double AttendancePercent { get; init; }
}

/// <summary>Payload stored in the outbox for SMS deliveries.</summary>
public sealed record SmsPayload(string Phone, string Message);
