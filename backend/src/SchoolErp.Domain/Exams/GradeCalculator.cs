namespace SchoolErp.Domain.Exams;

/// <summary>
/// CBSE-style grade bands over a 0–100 percentage. Kept as pure domain logic;
/// per-tenant configurable scales can replace this table later without
/// touching callers.
/// </summary>
public static class GradeCalculator
{
    /// <summary>Maps a percentage (0–100) to a grade band.</summary>
    public static string GradeFor(decimal percent) => percent switch
    {
        >= 91 => "A1",
        >= 81 => "A2",
        >= 71 => "B1",
        >= 61 => "B2",
        >= 51 => "C1",
        >= 41 => "C2",
        >= 33 => "D",
        _ => "E",
    };

    /// <summary>Percentage for a mark against a maximum, rounded to 2 decimals.</summary>
    public static decimal Percent(decimal marks, decimal maxMarks) =>
        maxMarks <= 0 ? 0 : Math.Round(marks * 100m / maxMarks, 2);
}
