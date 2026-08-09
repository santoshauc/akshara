namespace SchoolErp.AdminPortal.Models;

/// <summary>One day on the attendance trend.</summary>
public sealed record TrendPointDto(DateOnly Date, decimal Percent, int Marked);

/// <summary>One month of fee collections.</summary>
public sealed record MonthFeePointDto(string Month, decimal Collected);

/// <summary>One class's roll-call percentage this month.</summary>
public sealed record ClassAttendanceDto(string ClassName, decimal Percent, int Marked);

/// <summary>Average achievement across one published exam's papers.</summary>
public sealed record ExamAverageDto(string ExamName, decimal AveragePercent, int Entries);

/// <summary>The admissions pipeline by stage.</summary>
public sealed record EnquiryFunnelDto(int New, int Contacted, int Visit, int Admitted, int Lost);

/// <summary>One teacher's teaching-outcome numbers.</summary>
public sealed record TeacherInsightDto(
    Guid TeacherId,
    string Name,
    int PeriodsPerWeek,
    decimal? AveragePercent,
    decimal? DeltaVsSchool,
    int DaysAbsent,
    int MarksBacklog);

/// <summary>Everything the management insights page draws.</summary>
public sealed record ManagementInsightsDto(
    List<TrendPointDto> AttendanceTrend,
    List<MonthFeePointDto> FeeSeries,
    decimal FeesOutstanding,
    List<ClassAttendanceDto> ClassAttendance,
    List<ExamAverageDto> ExamAverages,
    EnquiryFunnelDto EnquiryFunnel,
    int SubstitutionsThisMonth);
