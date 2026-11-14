using System.ComponentModel;
using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.Api.Applications;

/// <summary>A stored application. The owner is never returned.</summary>
public sealed record ApplicationResponse(
    Guid Id,
    decimal AnnualIncome,
    decimal MonthlyExpenses,
    decimal DepositAmount,
    decimal PropertyValue,
    int TermYears,
    DateTimeOffset CreatedAt)
{
    public static ApplicationResponse From(MortgageApplication application) => new(
        application.Id,
        application.AnnualIncome,
        application.MonthlyExpenses,
        application.DepositAmount,
        application.PropertyValue,
        application.TermYears,
        application.CreatedAt);

    /// <summary>Only the ID: the amounts are personal financial data (rules section 6, security L1).</summary>
    public override string ToString() => $"{nameof(ApplicationResponse)} {{ Id = {Id} }}";
}

/// <summary>One assessment. <see cref="Result"/> is null unless the status is Completed (Phase 6 adds Pending).</summary>
public sealed record AssessmentResponse(
    Guid Id,
    Guid ApplicationId,
    AssessmentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AssessedAt,
    AssessmentResultResponse? Result)
{
    public static AssessmentResponse From(AffordabilityAssessment assessment) => new(
        assessment.Id,
        assessment.ApplicationId,
        assessment.Status,
        assessment.CreatedAt,
        assessment.AssessedAt,
        assessment.Result is null ? null : AssessmentResultResponse.From(assessment.Result));

    /// <summary>Logging allowlist fields only (rules section 6).</summary>
    public override string ToString() =>
        $"{nameof(AssessmentResponse)} {{ Id = {Id}, ApplicationId = {ApplicationId}, Status = {Status} }}";
}

/// <summary>The rules section 5 outputs. Money, percentages and the multiple have 2 decimal places.</summary>
public sealed record AssessmentResultResponse(
    decimal LoanAmount,
    decimal LoanToValuePercent,
    [property: Description("Repayment at the assessment rate over the term.")] decimal MonthlyPayment,
    decimal DebtToIncomePercent,
    decimal LoanToIncomeMultiple,
    [property: Description("Estimate of income-based maximum borrowing; ignores property value and deposit.")] decimal MaxBorrowing,
    Decision Decision,
    IReadOnlyList<CheckResponse> Checks,
    string Summary,
    [property: Description("Annual rate used for the payment, for example 0.06.")] decimal AssessmentRate,
    string RulesVersion)
{
    public static AssessmentResultResponse From(AffordabilityResult result) => new(
        result.LoanAmount,
        result.LoanToValuePercent,
        result.MonthlyPayment,
        result.DebtToIncomePercent,
        result.LoanToIncomeMultiple,
        result.MaxBorrowing,
        result.Decision,
        [.. result.Checks.Select(c => new CheckResponse(c.Name, c.Value, c.Limit, c.Passed))],
        result.Summary,
        result.AssessmentRate,
        result.RulesVersion);

    /// <summary>Logging allowlist fields only: the values and summary reveal the inputs (rules section 6).</summary>
    public override string ToString() =>
        $"{nameof(AssessmentResultResponse)} {{ Decision = {Decision}, RulesVersion = {RulesVersion} }}";
}

/// <summary>One check (LTV, DTI or LTI): its value, the configured limit and whether it passed.</summary>
public sealed record CheckResponse(string Name, decimal Value, decimal Limit, bool Passed)
{
    /// <summary>Leaves out the value, which reveals the inputs (rules section 6).</summary>
    public override string ToString() => $"{nameof(CheckResponse)} {{ Name = {Name}, Passed = {Passed} }}";
}
