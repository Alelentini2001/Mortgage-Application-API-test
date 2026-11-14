namespace MortgageApplications.Core.Affordability;

/// <summary>
/// The outputs of one assessment (rules section 5). Every money, percentage and multiple output is rounded
/// once to 2 dp with scale 2; <see cref="AssessmentRate"/> is stored as configured.
/// MaxBorrowing is income-based and ignores property value and deposit.
/// </summary>
/// <remarks>
/// ToString prints only the logging allowlist fields (Decision, RulesVersion): the values and Summary reveal the
/// applicant's inputs (rules section 6). Only the calculator should build results; AffordabilityAssessment.Complete
/// also checks the result is consistent.
/// </remarks>
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
    string RulesVersion)
{
    public override string ToString() =>
        $"{nameof(AffordabilityResult)} {{ Decision = {Decision}, RulesVersion = {RulesVersion} }}";
}
