namespace SchoolErp.Domain.Exams;

/// <summary>One paper's contribution to a semester result.</summary>
/// <param name="Credits">Credit weight of the paper.</param>
/// <param name="Percent">Marks obtained as a percentage of the maximum.</param>
/// <param name="IsAbsent">Absent counts as a zero-point attempt, not a skip.</param>
public readonly record struct CreditedPaper(int Credits, decimal Percent, bool IsAbsent);

/// <summary>A letter grade and the point it carries.</summary>
public readonly record struct CbcsGrade(string Letter, int Point);

/// <summary>
/// Grade points and GPA for the UGC's Choice Based Credit System, the scale
/// most Indian universities publish results on.
///
/// Kept separate from <see cref="GradeCalculator"/>, which maps CBSE-style
/// school bands (A1…E) and has no notion of a credit. A college result and a
/// school report card are different documents; sharing one band table would
/// make both wrong.
///
/// CAVEAT worth knowing before trusting a transcript printed from this: the
/// scale below is the UGC's RECOMMENDED one. Universities vary — some start
/// O at 91, some award five points at 50, some add a separate 'Ab'. There is
/// no per-institution scale configuration yet, so a university whose
/// ordinance differs will not match.
/// </summary>
public static class CbcsGradeCalculator
{
    /// <summary>Below this a paper is failed and carries no points.</summary>
    public const decimal PassPercent = 40m;

    /// <summary>UGC 10-point scale, highest band first.</summary>
    private static readonly (decimal MinPercent, string Letter, int Point)[] Bands =
    [
        (90m, "O", 10),   // Outstanding
        (80m, "A+", 9),   // Excellent
        (70m, "A", 8),    // Very good
        (60m, "B+", 7),   // Good
        (50m, "B", 6),    // Above average
        (45m, "C", 5),    // Average
        (40m, "P", 4),    // Pass
    ];

    /// <summary>Grade for a percentage. Absence is F, not an exemption.</summary>
    public static CbcsGrade GradeFor(decimal percent, bool isAbsent = false)
    {
        if (isAbsent)
        {
            return new CbcsGrade("Ab", 0);
        }

        foreach (var (minPercent, letter, point) in Bands)
        {
            if (percent >= minPercent)
            {
                return new CbcsGrade(letter, point);
            }
        }

        return new CbcsGrade("F", 0);
    }

    /// <summary>
    /// Σ(credits × grade point) ÷ Σ(credits), to two decimals.
    ///
    /// Failed and absent papers stay in the DENOMINATOR: a student who fails
    /// one paper of six has a lower SGPA than one who passed all six, which is
    /// the point. Dropping them would quietly reward failure. Returns null
    /// when no paper carries credits — a school's exam, or a college semester
    /// nobody has set credits on — because 0.00 would read as "everyone
    /// failed" rather than "not measured".
    /// </summary>
    public static decimal? Gpa(IEnumerable<CreditedPaper> papers)
    {
        var totalCredits = 0;
        var totalPoints = 0;

        foreach (var paper in papers)
        {
            if (paper.Credits <= 0)
            {
                continue;
            }

            totalCredits += paper.Credits;
            totalPoints += paper.Credits * GradeFor(paper.Percent, paper.IsAbsent).Point;
        }

        return totalCredits == 0
            ? null
            : Math.Round((decimal)totalPoints / totalCredits, 2, MidpointRounding.AwayFromZero);
    }
}
