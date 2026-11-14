using MortgageApplications.Core.Affordability;

namespace MortgageApplications.UnitTests.Affordability;

/// <summary>
/// U10–U15: the public rule set behind the API's 400 response. One source of bounds with InputGuard (P3-D2):
/// the parity theories run the same inputs through both.
/// </summary>
public sealed class ApplicationInputRulesTests
{
    // U11: one field changed from E1; just outside each rules section 1 bound, plus more than 2 dp.
    public static TheoryData<string, decimal> InvalidRows => new()
    {
        { nameof(AffordabilityInputs.AnnualIncome), 0.99m },
        { nameof(AffordabilityInputs.AnnualIncome), 10_000_000.01m },
        { nameof(AffordabilityInputs.AnnualIncome), 85_000.001m },
        { nameof(AffordabilityInputs.MonthlyExpenses), -0.01m },
        { nameof(AffordabilityInputs.MonthlyExpenses), 1_000_000.01m },
        { nameof(AffordabilityInputs.PropertyValue), 999.99m },
        { nameof(AffordabilityInputs.PropertyValue), 100_000_000.01m },
        { nameof(AffordabilityInputs.DepositAmount), -0.01m },
        { nameof(AffordabilityInputs.TermYears), 4m },
        { nameof(AffordabilityInputs.TermYears), 41m },
    };

    // U11: exact limits (inside).
    public static TheoryData<string, decimal> ValidRows => new()
    {
        { nameof(AffordabilityInputs.AnnualIncome), 1m },
        { nameof(AffordabilityInputs.AnnualIncome), 10_000_000m },
        { nameof(AffordabilityInputs.MonthlyExpenses), 0m },
        { nameof(AffordabilityInputs.MonthlyExpenses), 1_000_000m },
        { nameof(AffordabilityInputs.PropertyValue), 100_000_000m },
        { nameof(AffordabilityInputs.DepositAmount), 0m },
        { nameof(AffordabilityInputs.TermYears), 5m },
        { nameof(AffordabilityInputs.TermYears), 40m },
    };

    public static TheoryData<string> MoneyFields =>
    [
        nameof(AffordabilityInputs.AnnualIncome),
        nameof(AffordabilityInputs.MonthlyExpenses),
        nameof(AffordabilityInputs.DepositAmount),
        nameof(AffordabilityInputs.PropertyValue),
    ];

    // U10
    [Fact]
    public void Validate_valid_inputs_returns_no_errors()
    {
        var errors = ApplicationInputRules.Validate(TestData.E1);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_null_inputs_throws()
    {
        Assert.Throws<ArgumentNullException>(() => ApplicationInputRules.Validate(null!));
    }

    // U11: invalid rows give one error for that field, and the guard rejects the same field.
    [Theory]
    [MemberData(nameof(InvalidRows))]
    public void Validate_invalid_field_reports_that_field_like_the_guard(string field, decimal value)
    {
        var inputs = With(field, value);

        var errors = ApplicationInputRules.Validate(inputs);
        var guard = Assert.Throws<ArgumentOutOfRangeException>(() => TestData.Calculator().Assess(inputs));

        var error = Assert.Single(errors);
        Assert.Equal(field, error.Field);
        Assert.Equal(field, guard.ParamName);
    }

    // U11: valid rows give no errors, and the guard accepts them too.
    [Theory]
    [MemberData(nameof(ValidRows))]
    public void Validate_field_at_limit_is_accepted_like_the_guard(string field, decimal value)
    {
        var inputs = With(field, value);

        var errors = ApplicationInputRules.Validate(inputs);
        var result = TestData.Calculator().Assess(inputs);

        Assert.Empty(errors);
        Assert.NotNull(result);
    }

    // U12: loan = 200,000 − 199,000 = 1,000.00, exactly the minimum.
    [Fact]
    public void Validate_loan_exactly_minimum_is_accepted()
    {
        var inputs = TestData.E1 with { PropertyValue = 200_000m, DepositAmount = 199_000m };

        Assert.Empty(ApplicationInputRules.Validate(inputs));
    }

    [Fact]
    public void Validate_property_value_1000_with_zero_deposit_is_accepted()
    {
        var inputs = TestData.E1 with { PropertyValue = 1_000m, DepositAmount = 0m };

        Assert.Empty(ApplicationInputRules.Validate(inputs));
    }

    // U13: loan 999.99 and loan 0 are below the minimum; the error is on the deposit.
    [Theory]
    [MemberData(nameof(LoanBelowMinimumDeposits))]
    public void Validate_loan_below_minimum_reports_deposit(decimal deposit)
    {
        var inputs = TestData.E1 with { PropertyValue = 200_000m, DepositAmount = deposit };

        var error = Assert.Single(ApplicationInputRules.Validate(inputs));

        Assert.Equal(nameof(AffordabilityInputs.DepositAmount), error.Field);
    }

    public static TheoryData<decimal> LoanBelowMinimumDeposits => [199_000.01m, 200_000m];

    // A6: the minimum-loan rule only runs when property value and deposit are each valid.
    [Fact]
    public void Validate_invalid_property_value_does_not_add_a_loan_error()
    {
        var inputs = TestData.E1 with { PropertyValue = 999.99m, DepositAmount = 999.99m };

        var error = Assert.Single(ApplicationInputRules.Validate(inputs));

        Assert.Equal(nameof(AffordabilityInputs.PropertyValue), error.Field);
    }

    [Fact]
    public void Validate_invalid_deposit_does_not_add_a_loan_error()
    {
        var inputs = TestData.E1 with { DepositAmount = 300_000.001m };

        var error = Assert.Single(ApplicationInputRules.Validate(inputs));

        Assert.Equal(nameof(AffordabilityInputs.DepositAmount), error.Field);
    }

    // U14: every broken field is reported in one call, in field order.
    [Fact]
    public void Validate_several_invalid_fields_reports_each_once()
    {
        var inputs = TestData.E1 with { AnnualIncome = 0m, MonthlyExpenses = -1m, TermYears = 3 };

        var errors = ApplicationInputRules.Validate(inputs);

        Assert.Equal(
            [nameof(AffordabilityInputs.AnnualIncome), nameof(AffordabilityInputs.MonthlyExpenses), nameof(AffordabilityInputs.TermYears)],
            errors.Select(e => e.Field));
    }

    // U15: messages never carry the submitted value.
    [Theory]
    [MemberData(nameof(MoneyFields))]
    public void Validate_message_does_not_contain_the_value(string field)
    {
        var inputs = TestData.E1With(field, -123_456.789m);

        var errors = ApplicationInputRules.Validate(inputs);

        Assert.NotEmpty(errors);
        Assert.All(errors, e => Assert.DoesNotContain("123", e.Message, StringComparison.Ordinal));
        Assert.All(errors, e => Assert.False(string.IsNullOrWhiteSpace(e.Message)));
    }

    private static AffordabilityInputs With(string field, decimal value) =>
        field == nameof(AffordabilityInputs.TermYears)
            ? TestData.E1 with { TermYears = (int)value }
            : TestData.E1With(field, value);
}
