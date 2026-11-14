using System.Globalization;

namespace MortgageApplications.Core.Affordability;

/// <summary>
/// Builds the one-sentence explanation with the exact templates in rules section 6.
/// Only rounded check values and limits appear; never the applicant's raw inputs.
/// </summary>
internal static class SummaryBuilder
{
    public static string Build(IReadOnlyList<CheckResult> checks)
    {
        if (checks.All(c => c.Passed))
        {
            var ltv = checks[0];
            var dti = checks[1];
            var lti = checks[2];
            return $"Approved: all 3 checks passed (LTV {F(ltv.Value)}% ≤ {F(ltv.Limit)}%, " +
                $"DTI {F(dti.Value)}% ≤ {F(dti.Limit)}%, LTI {F(lti.Value)}× ≤ {F(lti.Limit)}×).";
        }

        var failures = checks.Where(c => !c.Passed).Select(Failure);
        return $"Declined: {string.Join("; ", failures)}.";
    }

    private static string Failure(CheckResult check)
    {
        var unit = check.Name == AffordabilityCalculator.LoanToIncomeCheck ? "×" : "%";

        // Values are compared unrounded (rules section 3), so a failure can display equal to its limit.
        var suffix = check.Value == check.Limit ? " before rounding" : string.Empty;
        return $"{check.Name} {F(check.Value)}{unit} is above the {F(check.Limit)}{unit} limit{suffix}";
    }

    private static string F(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
