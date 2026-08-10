using FluentAssertions;
using SchoolErp.Domain.Exams;

namespace SchoolErp.UnitTests.Exams;

/// <summary>The UGC 10-point band table, boundary by boundary.</summary>
public sealed class CbcsGradeBandTests
{
    [Theory]
    [InlineData(100, "O", 10)]
    [InlineData(90, "O", 10)]
    [InlineData(89.99, "A+", 9)]
    [InlineData(80, "A+", 9)]
    [InlineData(79.99, "A", 8)]
    [InlineData(70, "A", 8)]
    [InlineData(69.99, "B+", 7)]
    [InlineData(60, "B+", 7)]
    [InlineData(59.99, "B", 6)]
    [InlineData(50, "B", 6)]
    [InlineData(49.99, "C", 5)]
    [InlineData(45, "C", 5)]
    [InlineData(44.99, "P", 4)]
    [InlineData(40, "P", 4)]
    [InlineData(39.99, "F", 0)]
    [InlineData(0, "F", 0)]
    public void Percentages_map_to_the_ugc_scale(double percent, string letter, int point)
    {
        var grade = CbcsGradeCalculator.GradeFor((decimal)percent);

        grade.Letter.Should().Be(letter);
        grade.Point.Should().Be(point);
    }

    [Fact]
    public void Absence_is_a_zero_point_attempt_not_an_exemption()
    {
        // Marks of 95 with the absent flag set still scores nothing: the flag
        // is the authority, or a data-entry slip would hand out a grade.
        var grade = CbcsGradeCalculator.GradeFor(95m, isAbsent: true);

        grade.Letter.Should().Be("Ab");
        grade.Point.Should().Be(0);
    }
}

/// <summary>SGPA/CGPA arithmetic — the part a transcript is judged on.</summary>
public sealed class CbcsGpaTests
{
    [Fact]
    public void Gpa_is_credit_weighted_not_a_plain_average()
    {
        // A 4-credit A (8) and a 2-credit P (4). The plain average is 6.00;
        // credit-weighted it is (4×8 + 2×4) / 6 = 6.67.
        var papers = new[]
        {
            new CreditedPaper(4, 75m, false),
            new CreditedPaper(2, 42m, false),
        };

        CbcsGradeCalculator.Gpa(papers).Should().Be(6.67m);
    }

    [Fact]
    public void A_failed_paper_stays_in_the_denominator()
    {
        // Five 4-credit papers at O and one failed 4-credit paper:
        // (5×4×10 + 4×0) / 24 = 8.33. Dropping the failure would give 10.00.
        var papers = Enumerable.Repeat(new CreditedPaper(4, 95m, false), 5)
            .Append(new CreditedPaper(4, 20m, false))
            .ToList();

        CbcsGradeCalculator.Gpa(papers).Should().Be(8.33m);
    }

    [Fact]
    public void An_absent_paper_also_counts_against_the_student()
    {
        var papers = new[]
        {
            new CreditedPaper(4, 95m, false),
            new CreditedPaper(4, 0m, true),
        };

        CbcsGradeCalculator.Gpa(papers).Should().Be(5.00m);
    }

    [Fact]
    public void Papers_without_credits_are_not_counted()
    {
        // A zero-credit audit paper must not dilute the result.
        var papers = new[]
        {
            new CreditedPaper(4, 95m, false),
            new CreditedPaper(0, 10m, false),
        };

        CbcsGradeCalculator.Gpa(papers).Should().Be(10m);
    }

    [Fact]
    public void No_credited_paper_means_unavailable_rather_than_zero()
    {
        // A school's exam, or a semester nobody has set credits on. 0.00 would
        // read as "everyone failed".
        CbcsGradeCalculator.Gpa([new CreditedPaper(0, 88m, false)]).Should().BeNull();
        CbcsGradeCalculator.Gpa([]).Should().BeNull();
    }

    [Fact]
    public void A_perfect_semester_is_exactly_ten()
    {
        var papers = new[]
        {
            new CreditedPaper(3, 90m, false),
            new CreditedPaper(5, 100m, false),
        };

        CbcsGradeCalculator.Gpa(papers).Should().Be(10m);
    }
}
