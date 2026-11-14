using System.Globalization;

namespace MortgageApplications.Core.Affordability;

/// <summary>
/// Thresholds and rate for the affordability rules (rules section 1). Bound from configuration, so the
/// properties are settable and there are no defaults in code. Any change must bump <see cref="RulesVersion"/>.
/// </summary>
public sealed class AffordabilityOptions
{
    /// <summary>Annual assessment rate, for example 0.06 (6%).</summary>
    public decimal AssessmentRate { get; set; }

    public decimal MaxLoanToValuePercent { get; set; }

    public decimal MaxDebtToIncomePercent { get; set; }

    public decimal MaxLoanToIncomeMultiple { get; set; }

    public string? RulesVersion { get; set; }

    /// <summary>
    /// Checks every bound and returns one message per invalid option (empty when valid).
    /// Messages name the option and its bounds, never the configured value.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (AssessmentRate < 0.001m || AssessmentRate > 0.25m || !DecimalMath.HasAtMostDecimals(AssessmentRate, 4))
        {
            errors.Add($"{nameof(AssessmentRate)} must be between 0.001 and 0.25 with at most 4 decimal places.");
        }

        AddLimitError(errors, nameof(MaxLoanToValuePercent), MaxLoanToValuePercent, 100m);
        AddLimitError(errors, nameof(MaxDebtToIncomePercent), MaxDebtToIncomePercent, 100m);
        AddLimitError(errors, nameof(MaxLoanToIncomeMultiple), MaxLoanToIncomeMultiple, 10m);

        if (string.IsNullOrWhiteSpace(RulesVersion))
        {
            errors.Add($"{nameof(RulesVersion)} is required.");
        }

        return errors;
    }

    private static void AddLimitError(List<string> errors, string name, decimal value, decimal max)
    {
        if (value <= 0m || value > max || !DecimalMath.HasAtMostDecimals(value, 2))
        {
            errors.Add(string.Create(CultureInfo.InvariantCulture, $"{name} must be greater than 0 and at most {max} with at most 2 decimal places."));
        }
    }
}
