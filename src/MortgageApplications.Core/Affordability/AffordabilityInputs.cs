namespace MortgageApplications.Core.Affordability;

/// <summary>The five application fields the calculator reads (rules section 1).</summary>
public sealed record AffordabilityInputs(
    decimal AnnualIncome,
    decimal MonthlyExpenses,
    decimal DepositAmount,
    decimal PropertyValue,
    int TermYears);
