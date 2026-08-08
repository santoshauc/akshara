using SchoolErp.Domain.Exams;

namespace SchoolErp.AdminPortal.Models;

/// <summary>Subject (mirrors SubjectDto).</summary>
public sealed record SubjectDto(Guid Id, string Name, string Code);

/// <summary>Scheduled paper (mirrors ExamSubjectDto).</summary>
public sealed record ExamSubjectDto(
    Guid Id,
    Guid SchoolClassId,
    string ClassName,
    Guid SubjectId,
    string SubjectName,
    DateOnly? ExamDate,
    decimal MaxMarks,
    decimal PassMarks);

/// <summary>Exam with papers (mirrors ExamDto).</summary>
public sealed record ExamDto(
    Guid Id,
    string Name,
    Guid AcademicYearId,
    DateOnly StartDate,
    DateOnly EndDate,
    ExamStatus Status,
    List<ExamSubjectDto> Subjects);

/// <summary>Create-exam payload (mirrors CreateExamCommand).</summary>
public sealed record CreateExamRequest(
    string Name, Guid AcademicYearId, DateOnly StartDate, DateOnly EndDate);

/// <summary>Create-subject payload (mirrors CreateSubjectCommand).</summary>
public sealed record CreateSubjectRequest(string Name, string Code);

/// <summary>Paper-scheduling payload (mirrors SchedulePaperRequest).</summary>
public sealed record SchedulePaperRequest(
    Guid SchoolClassId, Guid SubjectId, DateOnly? ExamDate, decimal MaxMarks, decimal PassMarks);

/// <summary>One marks-grid row (mirrors MarksGridRowDto).</summary>
public sealed record MarksGridRowDto(
    Guid EnrollmentId,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    int? RollNumber,
    decimal? MarksObtained,
    bool IsAbsent);

/// <summary>The marks grid (mirrors MarksGridDto).</summary>
public sealed record MarksGridDto(
    Guid ExamSubjectId,
    string SubjectName,
    string ClassName,
    decimal MaxMarks,
    List<MarksGridRowDto> Rows);

/// <summary>One student's marks in a submission (mirrors MarkInput).</summary>
public sealed record MarkInputModel(Guid EnrollmentId, decimal? MarksObtained, bool IsAbsent);

/// <summary>Marks submission payload (mirrors EnterMarksRequest).</summary>
public sealed record EnterMarksRequest(List<MarkInputModel> Entries);

/// <summary>One subject line of a result (mirrors ResultLineDto).</summary>
public sealed record ResultLineDto(
    string SubjectName,
    decimal MaxMarks,
    decimal? MarksObtained,
    bool IsAbsent,
    string Grade,
    bool Passed);

/// <summary>A student's exam result (mirrors StudentResultDto).</summary>
public sealed record StudentResultDto(
    Guid StudentId,
    Guid ExamId,
    string ExamName,
    ExamStatus ExamStatus,
    List<ResultLineDto> Lines,
    decimal TotalMax,
    decimal TotalObtained,
    decimal Percent,
    string OverallGrade,
    int? SectionRank,
    int SectionSize);

/// <summary>Term report definition (mirrors Application TermReportDto).</summary>
public sealed record TermReportDto(
    Guid Id,
    string Name,
    Guid AcademicYearId,
    List<TermReportComponentDto> Components);

/// <summary>Weighted exam inside a definition.</summary>
public sealed record TermReportComponentDto(Guid ExamId, string ExamName, decimal WeightPercent);

/// <summary>Creation input line (mirrors TermReportComponentInput).</summary>
public sealed record TermReportComponentInput(Guid ExamId, decimal WeightPercent);
