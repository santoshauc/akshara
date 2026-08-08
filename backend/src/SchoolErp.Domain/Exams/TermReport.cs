using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Exams;

/// <summary>
/// A term/annual report definition: which exams count and with what weight.
/// The final card is computed at render time — no stored aggregates to drift.
/// </summary>
public class TermReport : TenantEntity
{
    public Guid AcademicYearId { get; set; }

    /// <summary>Display name (e.g. "Annual Report 2026-27").</summary>
    public string Name { get; set; } = string.Empty;

    public List<TermReportComponent> Components { get; set; } = [];
}

/// <summary>One weighted exam inside a term report. Weights sum to 100.</summary>
public class TermReportComponent : TenantEntity
{
    public Guid TermReportId { get; set; }

    public Guid ExamId { get; set; }

    public Exam? Exam { get; set; }

    /// <summary>Percentage contribution (0–100).</summary>
    public decimal WeightPercent { get; set; }
}

/// <summary>
/// Teacher-entered extras for one student on one term report:
/// co-scholastic grades (area → grade JSON) and free-text remarks.
/// </summary>
public class TermStudentInput : TenantEntity
{
    public Guid TermReportId { get; set; }

    public Guid StudentId { get; set; }

    /// <summary>JSON object of co-scholastic area → grade (e.g. {"Art":"A"}).</summary>
    public string? CoScholasticJson { get; set; }

    public string? Remarks { get; set; }
}
