using MortgageApplications.Core.Affordability;

namespace MortgageApplications.UnitTests.Affordability;

/// <summary>Exponentiation by squaring (D1–D3) and the 2 dp AwayFromZero rounding with scale 2 (R1–R2).</summary>
public sealed class DecimalMathTests
{
    public static TheoryData<decimal, int, decimal> ExactPowers => new()
    {
        { 7m, 0, 1m },
        { 7m, 1, 7m },
        { 2m, 10, 1024m },
        { 3m, 5, 243m },
        { 1.1m, 2, 1.21m },
        { 1.5m, 10, 57.6650390625m },
    };

    // Monthly growth factors over the real range: 6% for 5/30/40 years, 25% for 40 years (largest), 0.1% for 5 years.
    public static TheoryData<decimal, int> RealRangePowers => new()
    {
        { 1m + (0.06m / 12m), 60 },
        { 1m + (0.06m / 12m), 360 },
        { 1m + (0.06m / 12m), 480 },
        { 1m + (0.25m / 12m), 480 },
        { 1m + (0.001m / 12m), 60 },
    };

    public static TheoryData<decimal, decimal> AwayFromZero => new()
    {
        { 85.005m, 85.01m },
        { 0.125m, 0.13m },
        { 2.675m, 2.68m },
        { -0.005m, -0.01m },
        { 1.004999m, 1.00m },
        { 0.005m, 0.01m },
    };

    public static TheoryData<decimal> WholeNumbers => [80m, 80.0m, 240000m, 0m];

    [Theory]
    [MemberData(nameof(ExactPowers))]
    public void Pow_small_cases_are_exact(decimal value, int exponent, decimal expected)
    {
        Assert.Equal(expected, DecimalMath.Pow(value, exponent));
    }

    // Math.Pow is a test-only oracle here; Core never uses double.
    [Theory]
    [MemberData(nameof(RealRangePowers))]
    public void Pow_matches_double_oracle_within_relative_1e_12(decimal value, int exponent)
    {
        var result = DecimalMath.Pow(value, exponent);
        var oracle = (decimal)Math.Pow((double)value, exponent);

        Assert.True(Math.Abs(result - oracle) / result < 1e-12m);
    }

    [Fact]
    public void Pow_negative_exponent_throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => DecimalMath.Pow(2m, -1));

        Assert.Equal("exponent", ex.ParamName);
    }

    [Theory]
    [MemberData(nameof(AwayFromZero))]
    public void Round2_rounds_midpoints_away_from_zero(decimal value, decimal expected)
    {
        Assert.Equal(expected, DecimalMath.Round2(value));
    }

    [Theory]
    [MemberData(nameof(WholeNumbers))]
    public void Round2_returns_scale_2_without_changing_the_value(decimal value)
    {
        var rounded = DecimalMath.Round2(value);

        Assert.Equal(value, rounded);
        TestData.AssertScale2(rounded);
    }
}
