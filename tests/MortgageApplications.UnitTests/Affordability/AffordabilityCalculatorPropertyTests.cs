using MortgageApplications.Core.Affordability;

namespace MortgageApplications.UnitTests.Affordability;

/// <summary>
/// Seeded property tests (P2-D4). The generator stays inside the rules section 1 bounds, uses whole cents
/// (integer arithmetic, no double), and picks an exact bound for income, property, deposit, expenses and term
/// 10% of the time each. Small incomes clamp many properties to 1,000 (tiny loans); that is expected.
/// Assertions never depend on specific generated values, only on the invariants.
/// </summary>
public sealed class AffordabilityCalculatorPropertyTests
{
    private const int _seed = 20261114;
    private const int _samples = 10_000;

    // Calibrated from a real run (recorded in docs/progress/phase-02-core.md) so the invariant test can't pass vacuously.
    private const int _minimumApproved = 500;

    // P1: Approved ⇒ Loan ≤ MaxBorrowing (rules section 3).
    [Fact]
    public void Assess_approved_result_never_lends_more_than_max_borrowing()
    {
        var calculator = TestData.Calculator();
        var approved = 0;
        var violations = 0;

        foreach (var inputs in Generate())
        {
            var result = calculator.Assess(inputs);
            if (result.Decision == Decision.Approved)
            {
                approved++;
                if (result.LoanAmount > result.MaxBorrowing)
                {
                    violations++;
                }
            }
        }

        Assert.Equal(0, violations);
        Assert.True(approved >= _minimumApproved, $"Only {approved} of {_samples} samples were approved.");
    }

    // P2: total inside the bounds (no overflow, no division by zero), scale 2, MaxBorrowing ≥ 0, loan ≥ 1,000.
    [Fact]
    public void Assess_valid_inputs_never_throw_and_outputs_are_well_formed()
    {
        var calculator = TestData.Calculator();
        var malformed = 0;

        foreach (var inputs in Generate())
        {
            var r = calculator.Assess(inputs);
            decimal[] values = [r.LoanAmount, r.LoanToValuePercent, r.MonthlyPayment, r.DebtToIncomePercent, r.LoanToIncomeMultiple, r.MaxBorrowing];
            if (values.Any(v => v.Scale != 2) || r.MaxBorrowing < 0m || r.LoanAmount < 1_000.00m)
            {
                malformed++;
            }
        }

        Assert.Equal(0, malformed);
    }

    // P3: Approved exactly when all three checks pass.
    [Fact]
    public void Assess_decision_is_approved_exactly_when_all_checks_pass()
    {
        var calculator = TestData.Calculator();
        var mismatches = 0;

        foreach (var inputs in Generate())
        {
            var result = calculator.Assess(inputs);
            var allPassed = result.Checks.All(c => c.Passed);
            if (allPassed != (result.Decision == Decision.Approved))
            {
                mismatches++;
            }
        }

        Assert.Equal(0, mismatches);
    }

    private static IEnumerable<AffordabilityInputs> Generate()
    {
        var random = new Random(_seed);
        for (var i = 0; i < _samples; i++)
        {
            yield return Next(random);
        }
    }

    private static AffordabilityInputs Next(Random random)
    {
        // Income: log-uniform by decade over 1 to 10,000,000.
        var decade = random.Next(0, 7);
        var low = Pow10(decade) * 100;
        var income = Cents(random.NextInt64(low, (low * 10) + 1));
        income = Bias(random, income, 1m, 10_000_000m);

        // Property: income × 0.50–9.00, inside 1,000–100,000,000; 10% of the time an exact bound.
        var property = Clamp(Math.Round(income * Percent(random, 50, 900), 2), 1_000m, 100_000_000m);
        property = Bias(random, property, 1_000m, 100_000_000m);

        // Deposit: 0–40% of the property, always leaving a loan of at least 1,000.
        var deposit = Math.Round(property * Percent(random, 0, 40), 2);
        deposit = Bias(random, deposit, 0m, 0m);
        deposit = Math.Min(deposit, property - 1_000m);

        // Expenses: 0–60% of monthly income, inside 0–1,000,000.
        var expenses = Clamp(Math.Round(income / 12m * Percent(random, 0, 60), 2), 0m, 1_000_000m);
        expenses = Bias(random, expenses, 0m, 1_000_000m);

        var term = random.Next(5, 41);
        if (random.Next(10) == 0)
        {
            term = random.Next(2) == 0 ? 5 : 40;
        }

        return new AffordabilityInputs(income, expenses, deposit, property, term);
    }

    private static decimal Bias(Random random, decimal value, decimal lowerBound, decimal upperBound) =>
        random.Next(10) != 0 ? value : random.Next(2) == 0 ? lowerBound : upperBound;

    private static decimal Percent(Random random, int fromPercent, int toPercent) =>
        random.Next(fromPercent, toPercent + 1) / 100m;

    private static decimal Cents(long cents) => Math.Min(cents / 100m, 10_000_000m);

    private static decimal Clamp(decimal value, decimal min, decimal max) => Math.Max(min, Math.Min(max, value));

    private static long Pow10(int exponent)
    {
        long result = 1;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10;
        }

        return result;
    }
}
