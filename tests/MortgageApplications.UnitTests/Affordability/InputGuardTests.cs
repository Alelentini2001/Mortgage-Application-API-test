using System.Globalization;
using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.UnitTests.Affordability;

/// <summary>
/// Rules section 1 preconditions, tested through the public surface (the calculator and MortgageApplication.Create).
/// Each row changes one field of E1 (85,000 / 900 / 60,000 / 300,000 / 30y; loan 240,000).
/// </summary>
public sealed class InputGuardTests
{
    // V1 + V2 + V3: money fields out of range, more than 2 dp, or a loan below 1,000.
    public static TheoryData<string, decimal> InvalidMoney => new()
    {
        { nameof(AffordabilityInputs.AnnualIncome), 0.99m },
        { nameof(AffordabilityInputs.AnnualIncome), 0m },
        { nameof(AffordabilityInputs.AnnualIncome), -1m },
        { nameof(AffordabilityInputs.AnnualIncome), 10_000_000.01m },
        { nameof(AffordabilityInputs.AnnualIncome), 50_000.001m },
        { nameof(AffordabilityInputs.MonthlyExpenses), -0.01m },
        { nameof(AffordabilityInputs.MonthlyExpenses), 1_000_000.01m },
        { nameof(AffordabilityInputs.MonthlyExpenses), 0.001m },
        { nameof(AffordabilityInputs.PropertyValue), 999.99m },
        { nameof(AffordabilityInputs.PropertyValue), 100_000_000.01m },
        { nameof(AffordabilityInputs.PropertyValue), 300_000.001m },
        { nameof(AffordabilityInputs.DepositAmount), -0.01m },
        { nameof(AffordabilityInputs.DepositAmount), 30_000.001m },
        { nameof(AffordabilityInputs.DepositAmount), 299_000.01m },
        { nameof(AffordabilityInputs.DepositAmount), 300_000m },
        { nameof(AffordabilityInputs.DepositAmount), 310_000m },
    };

    // Exact limits and 2 dp values that must be accepted. 100.500m has scale 3 but value 100.5 (V7).
    public static TheoryData<string, decimal> ValidMoney => new()
    {
        { nameof(AffordabilityInputs.AnnualIncome), 1m },
        { nameof(AffordabilityInputs.AnnualIncome), 10_000_000m },
        { nameof(AffordabilityInputs.AnnualIncome), 50_000.01m },
        { nameof(AffordabilityInputs.AnnualIncome), 100.500m },
        { nameof(AffordabilityInputs.MonthlyExpenses), 0m },
        { nameof(AffordabilityInputs.MonthlyExpenses), 1_000_000m },
        { nameof(AffordabilityInputs.PropertyValue), 100_000_000m },
        { nameof(AffordabilityInputs.DepositAmount), 0m },
        { nameof(AffordabilityInputs.DepositAmount), 299_000m },
    };

    public static TheoryData<int> InvalidTerms => [4, 41, 0, -5];

    public static TheoryData<int> ValidTerms => [5, 40];

    [Theory]
    [MemberData(nameof(InvalidMoney))]
    public void Assess_invalid_money_field_throws_naming_that_field(string field, decimal value)
    {
        var inputs = TestData.E1With(field, value);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => TestData.Calculator().Assess(inputs));

        Assert.Equal(field, ex.ParamName);
    }

    [Theory]
    [MemberData(nameof(ValidMoney))]
    public void Assess_money_field_at_limit_is_accepted(string field, decimal value)
    {
        var inputs = TestData.E1With(field, value);

        var result = TestData.Calculator().Assess(inputs);

        Assert.NotNull(result);
    }

    [Fact]
    public void Assess_property_value_1000_with_zero_deposit_is_accepted()
    {
        var inputs = TestData.E1 with { PropertyValue = 1_000m, DepositAmount = 0m };

        var result = TestData.Calculator().Assess(inputs);

        Assert.Equal(1_000.00m, result.LoanAmount);
    }

    [Theory]
    [MemberData(nameof(InvalidTerms))]
    public void Assess_term_out_of_range_throws_naming_term(int termYears)
    {
        var inputs = TestData.E1 with { TermYears = termYears };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => TestData.Calculator().Assess(inputs));

        Assert.Equal(nameof(AffordabilityInputs.TermYears), ex.ParamName);
    }

    [Theory]
    [MemberData(nameof(ValidTerms))]
    public void Assess_term_at_limit_is_accepted(int termYears)
    {
        var result = TestData.Calculator().Assess(TestData.E1 with { TermYears = termYears });

        Assert.NotNull(result);
    }

    // V4: MortgageApplication.Create uses the same guard.
    public static TheoryData<string, decimal> InvalidForCreate => new()
    {
        { nameof(AffordabilityInputs.AnnualIncome), 0.99m },
        { nameof(AffordabilityInputs.DepositAmount), 299_000.01m },
        { nameof(AffordabilityInputs.MonthlyExpenses), -0.01m },
    };

    [Theory]
    [MemberData(nameof(InvalidForCreate))]
    public void Create_invalid_money_field_throws_naming_that_field(string field, decimal value)
    {
        var inputs = TestData.E1With(field, value);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MortgageApplication.Create("owner-1", inputs, TestData.CreatedAt));

        Assert.Equal(field, ex.ParamName);
    }

    [Fact]
    public void Create_term_out_of_range_throws_naming_term()
    {
        var inputs = TestData.E1 with { TermYears = 41 };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MortgageApplication.Create("owner-1", inputs, TestData.CreatedAt));

        Assert.Equal(nameof(AffordabilityInputs.TermYears), ex.ParamName);
    }

    // S1 + S2: the exception never carries the submitted value (no "Actual value was", no ActualValue).
    public static TheoryData<string, decimal, string> DistinctiveInvalidValues => new()
    {
        { nameof(AffordabilityInputs.AnnualIncome), 123_456_789.12m, "123456789" },
        { nameof(AffordabilityInputs.MonthlyExpenses), -987_654.32m, "987654" },
        { nameof(AffordabilityInputs.DepositAmount), 99_999_999.99m, "99999999" },
        { nameof(AffordabilityInputs.PropertyValue), 555_555_555.55m, "555555555" },
    };

    [Theory]
    [MemberData(nameof(DistinctiveInvalidValues))]
    public void Assess_guard_exception_does_not_contain_the_value(string field, decimal value, string digits)
    {
        var inputs = TestData.E1With(field, value);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => TestData.Calculator().Assess(inputs));

        AssertNoValueLeak(ex, field, value, digits);
    }

    [Theory]
    [MemberData(nameof(DistinctiveInvalidValues))]
    public void Create_guard_exception_does_not_contain_the_value(string field, decimal value, string digits)
    {
        var inputs = TestData.E1With(field, value);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MortgageApplication.Create("owner-1", inputs, TestData.CreatedAt));

        AssertNoValueLeak(ex, field, value, digits);
    }

    [Fact]
    public void Assess_term_guard_exception_does_not_contain_the_value()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => TestData.Calculator().Assess(TestData.E1 with { TermYears = 777 }));

        Assert.Null(ex.ActualValue);
        Assert.DoesNotContain("777", ex.Message, StringComparison.Ordinal);
    }

    // V5: null arguments.
    [Fact]
    public void Assess_null_inputs_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => TestData.Calculator().Assess(null!));
    }

    [Fact]
    public void Constructor_null_options_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => new AffordabilityCalculator(null!));
    }

    // V6: the calculator refuses invalid options.
    [Fact]
    public void Constructor_invalid_options_throws_argument_exception()
    {
        var options = TestData.Rules10();
        options.AssessmentRate = 0m;

        var ex = Assert.Throws<ArgumentException>(() => new AffordabilityCalculator(options));

        Assert.Contains(nameof(AffordabilityOptions.AssessmentRate), ex.Message, StringComparison.Ordinal);
    }

    private static void AssertNoValueLeak(ArgumentOutOfRangeException ex, string field, decimal value, string digits)
    {
        Assert.Equal(field, ex.ParamName);
        Assert.Null(ex.ActualValue);
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        Assert.DoesNotContain("Actual value", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(digits, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(value.ToString(CultureInfo.InvariantCulture), ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(value.ToString("N2", CultureInfo.InvariantCulture), ex.Message, StringComparison.Ordinal);
    }
}
