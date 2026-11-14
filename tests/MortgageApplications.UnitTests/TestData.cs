using MortgageApplications.Core.Affordability;

namespace MortgageApplications.UnitTests;

/// <summary>Shared builders. Options are always built explicitly: AffordabilityOptions has no defaults.</summary>
internal static class TestData
{
    public static readonly AffordabilityInputs E1 = new(85_000m, 900m, 60_000m, 300_000m, 30);

    public static readonly DateTimeOffset CreatedAt = new(2026, 11, 14, 9, 30, 0, TimeSpan.Zero);

    /// <summary>Rules version 1.0 (rules section 1): rate 6%, LTV 90%, DTI 45%, LTI 4.5×.</summary>
    public static AffordabilityOptions Rules10() => new()
    {
        AssessmentRate = 0.06m,
        MaxLoanToValuePercent = 90m,
        MaxDebtToIncomePercent = 45m,
        MaxLoanToIncomeMultiple = 4.5m,
        RulesVersion = "1.0",
    };

    public static AffordabilityCalculator Calculator() => new(Rules10());

    /// <summary>E1 with one money field changed (field named as in the guard's ParamName).</summary>
    public static AffordabilityInputs E1With(string field, decimal value) => field switch
    {
        nameof(AffordabilityInputs.AnnualIncome) => E1 with { AnnualIncome = value },
        nameof(AffordabilityInputs.MonthlyExpenses) => E1 with { MonthlyExpenses = value },
        nameof(AffordabilityInputs.DepositAmount) => E1 with { DepositAmount = value },
        nameof(AffordabilityInputs.PropertyValue) => E1 with { PropertyValue = value },
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };

    public static void AssertScale2(decimal value) => Assert.Equal(2, value.Scale);
}
