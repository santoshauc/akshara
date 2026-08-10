using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Exams;

public enum ExamStatus
{
    /// <summary>Being set up; marks are being entered.</summary>
    Draft = 1,
    /// <summary>Results are visible to parents; marks are frozen.</summary>
    Published = 2,
}

/// <summary>
/// An examination event ("Mid-Term 1 2026-27"). Its per-class subject
/// schedule lives in <see cref="ExamSubject"/> rows.
/// </summary>
public class Exam : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    public Guid AcademicYearId { get; set; }

    public AcademicYear? AcademicYear { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public ExamStatus Status { get; set; } = ExamStatus.Draft;

    public ICollection<ExamSubject> Subjects { get; set; } = [];
}

/// <summary>One scheduled paper: a subject for a class within an exam.</summary>
public class ExamSubject : TenantEntity
{
    public Guid ExamId { get; set; }

    public Guid SchoolClassId { get; set; }

    public SchoolClass? SchoolClass { get; set; }

    public Guid SubjectId { get; set; }

    public Subject? Subject { get; set; }

    public DateOnly? ExamDate { get; set; }

    public decimal MaxMarks { get; set; } = 100;

    public decimal PassMarks { get; set; } = 33;

    /// <summary>
    /// Credit weight of this paper, for colleges on a credit system. Null at a
    /// school, and at a college until someone sets it — which is why SGPA is
    /// reported as unavailable rather than zero when nothing carries credits.
    /// Lives on the paper, not the subject: the same subject is worth
    /// different credits in different programmes.
    /// </summary>
    public int? Credits { get; set; }
}

/// <summary>A student's marks for one paper. Upserted during entry; frozen on publish.</summary>
public class MarkEntry : TenantEntity
{
    public Guid ExamSubjectId { get; set; }

    public ExamSubject? ExamSubject { get; set; }

    public Guid EnrollmentId { get; set; }

    /// <summary>Denormalized for student-centric result queries.</summary>
    public Guid StudentId { get; set; }

    /// <summary>Null while absent; otherwise 0..MaxMarks.</summary>
    public decimal? MarksObtained { get; set; }

    public bool IsAbsent { get; set; }
}
