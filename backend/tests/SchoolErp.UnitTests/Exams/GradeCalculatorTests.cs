using FluentAssertions;
using SchoolErp.Domain.Exams;

namespace SchoolErp.UnitTests.Exams;

/// <summary>Band boundaries for the CBSE-style grade table.</summary>
public sealed class GradeCalculatorTests
{
    [Theory]
    [InlineData(100, "A1")]
    [InlineData(91, "A1")]
    [InlineData(90.99, "A2")]
    [InlineData(81, "A2")]
    [InlineData(71, "B1")]
    [InlineData(61, "B2")]
    [InlineData(51, "C1")]
    [InlineData(41, "C2")]
    [InlineData(33, "D")]
    [InlineData(32.99, "E")]
    [InlineData(0, "E")]
    public void Grades_map_to_the_correct_band(decimal percent, string expected) =>
        GradeCalculator.GradeFor(percent).Should().Be(expected);

    [Theory]
    [InlineData(45, 50, 90)]
    [InlineData(33, 100, 33)]
    [InlineData(1, 3, 33.33)]
    public void Percent_is_computed_and_rounded(decimal marks, decimal max, decimal expected) =>
        GradeCalculator.Percent(marks, max).Should().Be(expected);

    [Fact]
    public void Percent_of_zero_max_is_zero() =>
        GradeCalculator.Percent(10, 0).Should().Be(0);
}
