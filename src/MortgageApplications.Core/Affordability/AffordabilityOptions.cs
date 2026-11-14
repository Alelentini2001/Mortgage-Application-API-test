namespace MortgageApplications.Core.Affordability;

/// <summary>Thresholds and rate for the affordability rules. Bound from configuration; no defaults in code.</summary>
public sealed class AffordabilityOptions
{
    public decimal AssessmentRate { get; set; }

    public decimal MaxLoanToValuePercent { get; set; }

    public decimal MaxDebtToIncomePercent { get; set; }

    public decimal MaxLoanToIncomeMultiple { get; set; }

    public string? RulesVersion { get; set; }

    public IReadOnlyList<string> Validate() => throw new NotImplementedException();
}
