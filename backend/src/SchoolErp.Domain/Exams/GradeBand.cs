using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Exams;

/// <summary>
/// One band of an institution's own grading ordinance: "60% and above is B+,
/// worth 7 points".
///
/// Exists because the UGC 10-point scale is a RECOMMENDATION, not a rule.
/// Universities differ on where each grade starts and what it is worth, and a
/// transcript printed against the wrong scale is wrong in a way nobody
/// notices until a student disputes it. A tenant with no bands falls back to
/// the UGC default, so nothing changes for anyone who has not looked.
/// </summary>
public class GradeBand : TenantEntity
{
    /// <summary>Lowest percentage that earns this grade, inclusive.</summary>
    public decimal MinPercent { get; set; }

    /// <summary>Letter as the ordinance prints it — "O", "A+", "B", "P".</summary>
    public string Letter { get; set; } = string.Empty;

    /// <summary>Grade point the letter carries, on whatever scale the institution uses.</summary>
    public int Point { get; set; }
}
