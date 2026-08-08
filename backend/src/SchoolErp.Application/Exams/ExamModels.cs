using System.Linq.Expressions;
using SchoolErp.Domain.Exams;

namespace SchoolErp.Application.Exams;

/// <summary>Subject projection.</summary>
public sealed record SubjectDto(Guid Id, string Name, string Code);

/// <summary>Exam list/detail projection.</summary>
public sealed record ExamDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid AcademicYearId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public ExamStatus Status { get; init; }
    public IReadOnlyList<ExamSubjectDto> Subjects { get; init; } = [];
}

/// <summary>Scheduled paper projection.</summary>
public sealed record ExamSubjectDto
{
    public Guid Id { get; init; }
    public Guid SchoolClassId { get; init; }
    public string ClassName { get; init; } = string.Empty;
    public Guid SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public DateOnly? ExamDate { get; init; }
    public decimal MaxMarks { get; init; }
    public decimal PassMarks { get; init; }
}

/// <summary>One row of the marks-entry grid.</summary>
public sealed record MarksGridRowDto
{
    public Guid EnrollmentId { get; init; }
    public Guid StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string AdmissionNumber { get; init; } = string.Empty;
    public int? RollNumber { get; init; }
    public decimal? MarksObtained { get; init; }
    public bool IsAbsent { get; init; }
}

/// <summary>The marks-entry grid for one paper.</summary>
public sealed record MarksGridDto
{
    public Guid ExamSubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public decimal MaxMarks { get; init; }
    public IReadOnlyList<MarksGridRowDto> Rows { get; init; } = [];
}

/// <summary>One subject line of a student's result.</summary>
public sealed record ResultLineDto(
    string SubjectName,
    decimal MaxMarks,
    decimal? MarksObtained,
    bool IsAbsent,
    string Grade,
    bool Passed);

/// <summary>A student's computed result for one exam.</summary>
public sealed record StudentResultDto
{
    public Guid StudentId { get; init; }
    public Guid ExamId { get; init; }
    public string ExamName { get; init; } = string.Empty;
    public ExamStatus ExamStatus { get; init; }
    public IReadOnlyList<ResultLineDto> Lines { get; init; } = [];
    public decimal TotalMax { get; init; }
    public decimal TotalObtained { get; init; }
    public decimal Percent { get; init; }
    public string OverallGrade { get; init; } = string.Empty;
    /// <summary>1-based rank within the student's section, by total marks.</summary>
    public int? SectionRank { get; init; }
    public int SectionSize { get; init; }
}

/// <summary>Hand-written projections (EF-translatable expressions + in-memory maps).</summary>
public static class ExamMappings
{
    /// <summary>EF-translatable subject projection.</summary>
    public static readonly Expression<Func<Domain.Academics.Subject, SubjectDto>> SubjectProjection =
        subject => new SubjectDto(subject.Id, subject.Name, subject.Code);

    /// <summary>EF-translatable exam projection with its papers.</summary>
    public static readonly Expression<Func<Exam, ExamDto>> ExamProjection =
        exam => new ExamDto
        {
            Id = exam.Id,
            Name = exam.Name,
            AcademicYearId = exam.AcademicYearId,
            StartDate = exam.StartDate,
            EndDate = exam.EndDate,
            Status = exam.Status,
            Subjects = exam.Subjects
                .OrderBy(s => s.ExamDate)
                .Select(s => new ExamSubjectDto
                {
                    Id = s.Id,
                    SchoolClassId = s.SchoolClassId,
                    ClassName = s.SchoolClass!.Name,
                    SubjectId = s.SubjectId,
                    SubjectName = s.Subject!.Name,
                    ExamDate = s.ExamDate,
                    MaxMarks = s.MaxMarks,
                    PassMarks = s.PassMarks,
                })
                .ToList(),
        };

    public static SubjectDto ToDto(this Domain.Academics.Subject subject) =>
        new(subject.Id, subject.Name, subject.Code);
}
