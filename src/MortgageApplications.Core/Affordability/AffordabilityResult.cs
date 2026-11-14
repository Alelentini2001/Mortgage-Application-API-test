namespace MortgageApplications.Core.Affordability;

/// <summary>
/// The outputs of one assessment (rules section 5). Every decimal is rounded once to 2 dp with scale 2.
/// MaxBorrowing is income-based and ignores property value and deposit.
/// </summary>
public sealed record AffordabilityResult(
    decimal LoanAmount,
    decimal LoanToValuePercent,
    decimal MonthlyPayment,
    decimal DebtToIncomePercent,
    decimal LoanToIncomeMultiple,
    decimal MaxBorrowing,
    Decision Decision,
    IReadOnlyList<CheckResult> Checks,
    string Summary,
    decimal AssessmentRate,
    string RulesVersion);
