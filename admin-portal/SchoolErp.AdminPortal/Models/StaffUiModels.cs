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

/// <summary>Leave request row (mirrors Application LeaveRequestDto).</summary>
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

public enum LeaveRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

public enum LeaveApplicantKind
{
    Student = 1,
    Staff = 2,
}

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

/// <summary>Message row (mirrors StudentMessageDto).</summary>
public sealed record StudentMessageDto(
    Guid Id,
    bool SentByStaff,
    string SenderName,
    string Body,
    DateTimeOffset SentAt,
    bool Read);

/// <summary>Staff inbox row (mirrors MessageThreadDto).</summary>
public sealed record MessageThreadDto(
    Guid StudentId,
    string StudentName,
    string? ClassName,
    string LastMessage,
    DateTimeOffset LastMessageAt,
    int UnreadForStaff);

/// <summary>Upcoming exam tile line (mirrors UpcomingExamDto).</summary>
public sealed record UpcomingExamDto(string Name, DateOnly StartDate);

/// <summary>Dashboard numbers (mirrors DashboardDto).</summary>
public sealed record DashboardDto(
    int ActiveStudents,
    int AttendanceMarkedToday,
    int PresentToday,
    decimal AttendanceTodayPercent,
    decimal FeesCollectedThisMonth,
    int OverdueLoans,
    int PendingLeaveRequests,
    int UnreadParentMessages,
    int OpenEnquiries,
    int EnquiryFollowUpsDueToday,
    List<UpcomingExamDto> UpcomingExams,
    List<DashboardPointDto> AttendanceTrend,
    List<DashboardPointDto> FeeTrend,
    List<BirthdayDto> BirthdaysToday,
    decimal FeesOutstanding,
    int SmsCredits,
    DateOnly? SubscriptionExpiresOn);

/// <summary>One point on a small dashboard trend.</summary>
public sealed record DashboardPointDto(DateOnly Date, decimal Value);

/// <summary>A student celebrating today.</summary>
public sealed record BirthdayDto(string Name, string? ClassName, int TurnsAge);

/// <summary>One slot in the signed-in teacher's day (mirrors TeacherPeriodDto).</summary>
public sealed record TeacherPeriodDto(
    int Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string SubjectName,
    string ClassName,
    string? SectionName,
    bool IsSubstitution,
    bool AttendanceMarked);

/// <summary>An exam paper of theirs with no marks yet (mirrors TeacherMarksTaskDto).</summary>
public sealed record TeacherMarksTaskDto(
    string ExamName, string SubjectName, string ClassName, DateOnly ExamStartDate);

/// <summary>Homework due for a class they teach (mirrors TeacherHomeworkTaskDto).</summary>
public sealed record TeacherHomeworkTaskDto(
    string Title, string SubjectName, string ClassName, string? SectionName, DateOnly DueDate);

/// <summary>The teacher's own working day (mirrors MyTeacherDayDto).</summary>
public sealed record MyTeacherDayDto(
    string TeacherName,
    string EmployeeCode,
    DateOnly Date,
    IReadOnlyList<TeacherPeriodDto> Periods,
    IReadOnlyList<TeacherMarksTaskDto> MarksBacklog,
    IReadOnlyList<TeacherHomeworkTaskDto> HomeworkDue,
    int SectionsAwaitingAttendance,
    int PendingLeaveForMyStudents,
    int StudentsTaught);
