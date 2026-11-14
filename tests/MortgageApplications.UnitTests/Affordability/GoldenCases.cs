using MortgageApplications.Core.Affordability;
using static MortgageApplications.Core.Affordability.Decision;

namespace MortgageApplications.UnitTests.Affordability;

internal sealed record GoldenCase(
    AffordabilityInputs Inputs,
    decimal Loan,
    decimal Ltv,
    decimal Payment,
    decimal Dti,
    decimal Lti,
    decimal MaxBorrowing,
    Decision Decision,
    bool LtvPassed,
    bool DtiPassed,
    bool LtiPassed);

/// <summary>
/// Golden examples E1–E18 copied from docs/spec/affordability-rules.md section 8, plus E19
/// (mixed normal and "before rounding" failures, approved at P02-04). Do not recompute these.
/// </summary>
public static class GoldenCases
{
    internal static readonly IReadOnlyDictionary<string, GoldenCase> All = new Dictionary<string, GoldenCase>
    {
        ["E1"] = new(new(85_000m, 900m, 60_000m, 300_000m, 30), 240_000.00m, 80.00m, 1_438.92m, 33.02m, 2.82m, 381_535.82m, Approved, true, true, true),
        ["E2"] = new(new(85_000m, 900m, 20_000m, 300_000m, 30), 280_000.00m, 93.33m, 1_678.74m, 36.41m, 3.29m, 381_535.82m, Declined, false, true, true),
        ["E3"] = new(new(45_000m, 1_500m, 60_000m, 300_000m, 25), 240_000.00m, 80.00m, 1_546.32m, 81.24m, 5.33m, 29_101.29m, Declined, true, false, false),
        ["E4"] = new(new(60_000m, 0m, 30_000m, 200_000m, 25), 170_000.00m, 85.00m, 1_095.31m, 21.91m, 2.83m, 270_000.00m, Approved, true, true, true),
        ["E5"] = new(new(30_000m, 1_200m, 50_000m, 200_000m, 25), 150_000.00m, 75.00m, 966.45m, 86.66m, 5.00m, 0.00m, Declined, true, false, false),
        ["E6"] = new(new(50_000m, 0m, 60_000m, 300_000m, 35), 240_000.00m, 80.00m, 1_368.46m, 32.84m, 4.80m, 225_000.00m, Declined, true, true, false),
        ["E7"] = new(new(70_000m, 500m, 30_000m, 300_000m, 30), 270_000.00m, 90.00m, 1_618.79m, 36.32m, 3.86m, 315_000.00m, Approved, true, true, true),
        ["E8"] = new(new(60_000m, 500m, 30_000m, 300_000m, 30), 270_000.00m, 90.00m, 1_618.79m, 42.38m, 4.50m, 270_000.00m, Approved, true, true, true),
        ["E9"] = new(new(80_000m, 1_000m, 0m, 200_000m, 30), 200_000.00m, 100.00m, 1_199.10m, 32.99m, 2.50m, 333_583.23m, Declined, false, true, true),
        ["E10"] = new(new(60_000m, 0m, 29_990m, 200_000m, 25), 170_010.00m, 85.01m, 1_095.38m, 21.91m, 2.83m, 270_000.00m, Approved, true, true, true),
        ["E11"] = new(new(60_000m, 631.22m, 30_000m, 300_000m, 30), 270_000.00m, 90.00m, 1_618.79m, 45.00m, 4.50m, 269_998.93m, Declined, true, false, true),
        ["E12"] = new(new(60_000m, 631.21m, 30_000m, 300_000m, 30), 270_000.00m, 90.00m, 1_618.79m, 45.00m, 4.50m, 270_000.00m, Approved, true, true, true),
        ["E13"] = new(new(60_000m, 500m, 29_988m, 300_000m, 30), 270_012.00m, 90.00m, 1_618.86m, 42.38m, 4.50m, 270_000.00m, Declined, false, true, false),
        ["E14"] = new(new(60_000m, 1_625m, 30_000m, 300_000m, 30), 270_000.00m, 90.00m, 1_618.79m, 64.88m, 4.50m, 104_244.76m, Declined, true, false, true),
        ["E15"] = new(new(60_000m, 300m, 50_000m, 200_000m, 5), 150_000.00m, 75.00m, 2_899.92m, 64.00m, 2.50m, 100_864.84m, Declined, true, false, true),
        ["E16"] = new(new(40_000m, 200m, 20_000m, 180_000m, 40), 160_000.00m, 88.89m, 880.34m, 32.41m, 4.00m, 180_000.00m, Approved, true, true, true),
        ["E17"] = new(new(10_000_000m, 1_000_000m, 0m, 100_000_000m, 40), 100_000_000.00m, 100.00m, 550_213.64m, 186.03m, 10.00m, 0.00m, Declined, false, false, false),
        ["E18"] = new(new(1m, 1_000_000m, 0m, 100_000_000m, 5), 100_000_000.00m, 100.00m, 1_933_280.15m, 3_519_936_183.53m, 100_000_000.00m, 0.00m, Declined, false, false, false),
        ["E19"] = new(new(60_000m, 500m, 19_988m, 290_000m, 30), 270_012.00m, 93.11m, 1_618.86m, 42.38m, 4.50m, 270_000.00m, Declined, false, true, false),
    };

    public static TheoryData<string> Ids
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var id in All.Keys)
            {
                data.Add(id);
            }

            return data;
        }
    }

    /// <summary>Exact summaries: rules section 8 (E1, E2, E3, E6, E8, E11, E13, E17) and the template-derived G3 set + E19.</summary>
    public static TheoryData<string, string> Summaries => new()
    {
        { "E1", "Approved: all 3 checks passed (LTV 80.00% ≤ 90.00%, DTI 33.02% ≤ 45.00%, LTI 2.82× ≤ 4.50×)." },
        { "E2", "Declined: LTV 93.33% is above the 90.00% limit." },
        { "E3", "Declined: DTI 81.24% is above the 45.00% limit; LTI 5.33× is above the 4.50× limit." },
        { "E6", "Declined: LTI 4.80× is above the 4.50× limit." },
        { "E8", "Approved: all 3 checks passed (LTV 90.00% ≤ 90.00%, DTI 42.38% ≤ 45.00%, LTI 4.50× ≤ 4.50×)." },
        { "E11", "Declined: DTI 45.00% is above the 45.00% limit before rounding." },
        { "E13", "Declined: LTV 90.00% is above the 90.00% limit before rounding; LTI 4.50× is above the 4.50× limit before rounding." },
        { "E17", "Declined: LTV 100.00% is above the 90.00% limit; DTI 186.03% is above the 45.00% limit; LTI 10.00× is above the 4.50× limit." },
        { "E5", "Declined: DTI 86.66% is above the 45.00% limit; LTI 5.00× is above the 4.50× limit." },
        { "E9", "Declined: LTV 100.00% is above the 90.00% limit." },
        { "E12", "Approved: all 3 checks passed (LTV 90.00% ≤ 90.00%, DTI 45.00% ≤ 45.00%, LTI 4.50× ≤ 4.50×)." },
        { "E14", "Declined: DTI 64.88% is above the 45.00% limit." },
        { "E15", "Declined: DTI 64.00% is above the 45.00% limit." },
        { "E18", "Declined: LTV 100.00% is above the 90.00% limit; DTI 3519936183.53% is above the 45.00% limit; LTI 100000000.00× is above the 4.50× limit." },
        { "E19", "Declined: LTV 93.11% is above the 90.00% limit; LTI 4.50× is above the 4.50× limit before rounding." },
    };
}
