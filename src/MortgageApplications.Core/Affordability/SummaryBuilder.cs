using System.Globalization;

namespace MortgageApplications.Core.Affordability;

/// <summary>
/// Builds the one-sentence explanation with the exact templates in rules section 6.
/// Only rounded check values and limits appear; never the applicant's raw inputs.
/// </summary>
internal static class SummaryBuilder
{
    public static string Build(Decision decision, IReadOnlyList<CheckResult> checks)
    {
        if (decision == Decision.Approved)
        {
            var ltv = checks[0];
            var dti = checks[1];
            var lti = checks[2];
            return $"Approved: all 3 checks passed (LTV {F(ltv.Value)}% ≤ {F(ltv.Limit)}%, " +
                $"DTI {F(dti.Value)}% ≤ {F(dti.Limit)}%, LTI {F(lti.Value)}× ≤ {F(lti.Limit)}×).";
        }

        // Units follow the fixed check order LTV, DTI, LTI (rules section 5).
        string[] units = ["%", "%", "×"];
        var failures = checks.Select((check, i) => (check, unit: units[i])).Where(x => !x.check.Passed).Select(x => Failure(x.check, x.unit));
        return $"Declined: {string.Join("; ", failures)}.";
    }

    private static string Failure(CheckResult check, string unit)
    {
        // Values are compared unrounded (rules section 3), so a failure can display equal to its limit.
        var suffix = check.Value == check.Limit ? " before rounding" : string.Empty;
        return $"{check.Name} {F(check.Value)}{unit} is above the {F(check.Limit)}{unit} limit{suffix}";
    }

    private static string F(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
