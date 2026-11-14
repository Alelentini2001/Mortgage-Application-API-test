using System.Globalization;
using MortgageApplications.Core.Affordability;

namespace MortgageApplications.UnitTests.Affordability;

public sealed class AffordabilityCalculatorTests
{
    // G1: golden outputs (rules section 8), cited, never recomputed.
    [Theory]
    [MemberData(nameof(GoldenCases.Ids), MemberType = typeof(GoldenCases))]
    public void Assess_golden_case_returns_rules_section_8_outputs(string id)
    {
        var golden = GoldenCases.All[id];

        var result = TestData.Calculator().Assess(golden.Inputs);

        Assert.Equal(golden.Loan, result.LoanAmount);
        Assert.Equal(golden.Ltv, result.LoanToValuePercent);
        Assert.Equal(golden.Payment, result.MonthlyPayment);
        Assert.Equal(golden.Dti, result.DebtToIncomePercent);
        Assert.Equal(golden.Lti, result.LoanToIncomeMultiple);
        Assert.Equal(golden.MaxBorrowing, result.MaxBorrowing);
        Assert.Equal(golden.Decision, result.Decision);
    }

    [Theory]
    [MemberData(nameof(GoldenCases.Ids), MemberType = typeof(GoldenCases))]
    public void Assess_golden_case_returns_three_checks_in_order_with_values_limits_and_outcomes(string id)
    {
        var golden = GoldenCases.All[id];

        var checks = TestData.Calculator().Assess(golden.Inputs).Checks;

        Assert.Collection(
            checks,
            c => Assert.Equal(new CheckResult("LTV", golden.Ltv, 90.00m, golden.LtvPassed), c),
            c => Assert.Equal(new CheckResult("DTI", golden.Dti, 45.00m, golden.DtiPassed), c),
            c => Assert.Equal(new CheckResult("LTI", golden.Lti, 4.50m, golden.LtiPassed), c));
    }

    [Theory]
    [MemberData(nameof(GoldenCases.Ids), MemberType = typeof(GoldenCases))]
    public void Assess_golden_case_returns_every_decimal_with_scale_2(string id)
    {
        var result = TestData.Calculator().Assess(GoldenCases.All[id].Inputs);

        TestData.AssertScale2(result.LoanAmount);
        TestData.AssertScale2(result.LoanToValuePercent);
        TestData.AssertScale2(result.MonthlyPayment);
        TestData.AssertScale2(result.DebtToIncomePercent);
        TestData.AssertScale2(result.LoanToIncomeMultiple);
        TestData.AssertScale2(result.MaxBorrowing);
        Assert.All(result.Checks, c =>
        {
            TestData.AssertScale2(c.Value);
            TestData.AssertScale2(c.Limit);
        });
    }

    [Fact]
    public void Assess_stamps_rules_version_and_rate_from_options()
    {
        var result = TestData.Calculator().Assess(TestData.E1);

        Assert.Equal("1.0", result.RulesVersion);
        Assert.Equal(0.06m, result.AssessmentRate);
    }

    // G2 + G3 + E19: exact summaries, character for character.
    [Theory]
    [MemberData(nameof(GoldenCases.Summaries), MemberType = typeof(GoldenCases))]
    public void Assess_golden_case_returns_exact_summary(string id, string expectedSummary)
    {
        var result = TestData.Calculator().Assess(GoldenCases.All[id].Inputs);

        Assert.Equal(expectedSummary, result.Summary);
    }

    // B4: a passing value that rounds up to its limit gets no "before rounding".
    [Fact]
    public void Assess_ltv_just_below_limit_that_rounds_to_limit_passes()
    {
        var inputs = new AffordabilityInputs(60_000m, 500m, 30_001m, 300_000m, 30);

        var result = TestData.Calculator().Assess(inputs);

        Assert.Equal(new CheckResult("LTV", 90.00m, 90.00m, true), result.Checks[0]);
        Assert.DoesNotContain("before rounding", result.Summary, StringComparison.Ordinal);
    }

    // B5: minimum loan of exactly 1,000.
    [Fact]
    public void Assess_minimum_loan_of_1000_is_approved()
    {
        var inputs = new AffordabilityInputs(85_000m, 900m, 299_000m, 300_000m, 30);

        var result = TestData.Calculator().Assess(inputs);

        Assert.Equal(1_000.00m, result.LoanAmount);
        Assert.Equal(0.33m, result.LoanToValuePercent);
        Assert.Equal(0.01m, result.LoanToIncomeMultiple);
        TestData.AssertScale2(result.LoanToValuePercent);
        TestData.AssertScale2(result.LoanToIncomeMultiple);
        Assert.Equal(Decision.Approved, result.Decision);
    }

    // B6: AffordablePay exactly 0 clamps MaxBorrowing to 0.
    [Fact]
    public void Assess_expenses_exactly_at_dti_limit_gives_max_borrowing_zero()
    {
        var inputs = new AffordabilityInputs(60_000m, 2_250m, 30_000m, 300_000m, 30);

        var result = TestData.Calculator().Assess(inputs);

        Assert.Equal(0.00m, result.MaxBorrowing);
        TestData.AssertScale2(result.MaxBorrowing);
        Assert.Equal(Decision.Declined, result.Decision);
        Assert.False(result.Checks[1].Passed);
        Assert.Equal("Declined: DTI 77.38% is above the 45.00% limit.", result.Summary);
    }

    // B7: thresholds come from options; MaxBorrowing ignores the LTV limit.
    [Fact]
    public void Assess_uses_ltv_limit_from_options()
    {
        var options = TestData.Rules10();
        options.MaxLoanToValuePercent = 89.99m;

        var result = new AffordabilityCalculator(options).Assess(GoldenCases.All["E7"].Inputs);

        Assert.Equal(Decision.Declined, result.Decision);
        Assert.Equal(new CheckResult("LTV", 90.00m, 89.99m, false), result.Checks[0]);
        Assert.Equal(315_000.00m, result.MaxBorrowing);
        Assert.Equal("Declined: LTV 90.00% is above the 89.99% limit.", result.Summary);
    }

    // B8: the rate, limits and version used are stored on the result.
    [Fact]
    public void Assess_with_custom_options_stores_rate_limits_and_version()
    {
        var options = new AffordabilityOptions
        {
            AssessmentRate = 0.07m,
            MaxLoanToValuePercent = 85m,
            MaxDebtToIncomePercent = 40m,
            MaxLoanToIncomeMultiple = 4m,
            RulesVersion = "2.0",
        };

        var result = new AffordabilityCalculator(options).Assess(TestData.E1);

        Assert.Equal("2.0", result.RulesVersion);
        Assert.Equal(0.07m, result.AssessmentRate);
        Assert.Equal([85.00m, 40.00m, 4.00m], result.Checks.Select(c => c.Limit));
        Assert.All(result.Checks, c => TestData.AssertScale2(c.Limit));
        Assert.Equal(
            "Approved: all 3 checks passed (LTV 80.00% ≤ 85.00%, DTI 35.25% ≤ 40.00%, LTI 2.82× ≤ 4.00×).",
            result.Summary);
    }

    // O4: the calculator copies the options; later changes don't affect it.
    [Fact]
    public void Assess_ignores_changes_to_options_after_construction()
    {
        var options = TestData.Rules10();
        var calculator = new AffordabilityCalculator(options);

        options.MaxLoanToValuePercent = 10m;
        options.RulesVersion = "changed";
        var result = calculator.Assess(TestData.E1);

        Assert.Equal(Decision.Approved, result.Decision);
        Assert.Equal(90.00m, result.Checks[0].Limit);
        Assert.Equal("1.0", result.RulesVersion);
    }

    // C1: summary is culture-independent. Named cultures are unavailable under InvariantGlobalization,
    // so clone the invariant culture and swap the separators.
    [Fact]
    public void Assess_summary_is_the_same_under_a_comma_decimal_culture()
    {
        var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        comma.NumberFormat.NumberDecimalSeparator = ",";
        comma.NumberFormat.NumberGroupSeparator = ".";
        comma.NumberFormat.PercentDecimalSeparator = ",";
        comma.NumberFormat.PercentGroupSeparator = ".";
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = comma;
            CultureInfo.CurrentUICulture = comma;
            Assert.Equal("1,5", 1.5m.ToString(CultureInfo.CurrentCulture));

            var e1 = TestData.Calculator().Assess(GoldenCases.All["E1"].Inputs);
            var e17 = TestData.Calculator().Assess(GoldenCases.All["E17"].Inputs);

            Assert.Equal("Approved: all 3 checks passed (LTV 80.00% ≤ 90.00%, DTI 33.02% ≤ 45.00%, LTI 2.82× ≤ 4.50×).", e1.Summary);
            Assert.Equal("Declined: LTV 100.00% is above the 90.00% limit; DTI 186.03% is above the 45.00% limit; LTI 10.00× is above the 4.50× limit.", e17.Summary);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    // X2: one instance, many threads (it is a singleton from Phase 3).
    [Fact]
    public void Assess_is_safe_to_call_from_many_threads_on_one_instance()
    {
        var calculator = TestData.Calculator();
        var e1 = GoldenCases.All["E1"];
        var e3 = GoldenCases.All["E3"];
        var mismatches = 0;

        Parallel.For(0, 1_000, i =>
        {
            var golden = i % 2 == 0 ? e1 : e3;
            var result = calculator.Assess(golden.Inputs);
            if (result.MonthlyPayment != golden.Payment || result.MaxBorrowing != golden.MaxBorrowing || result.Decision != golden.Decision)
            {
                Interlocked.Increment(ref mismatches);
            }
        });

        Assert.Equal(0, mismatches);
    }
}
