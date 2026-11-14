namespace MortgageApplications.Core.Affordability;

/// <summary>
/// Pure affordability calculator (rules v1.0, docs/spec/affordability-rules.md). A function of
/// (inputs, options) only: no clock, configuration reads, I/O or logging. All arithmetic is decimal,
/// computed in the order of rules section 2; checks compare full-precision values and outputs are
/// rounded once at the end. Holds no mutable state, so one instance is safe to share across threads.
/// </summary>
public sealed class AffordabilityCalculator
{
    public const string LoanToValueCheck = "LTV";
    public const string DebtToIncomeCheck = "DTI";
    public const string LoanToIncomeCheck = "LTI";

    private readonly decimal _assessmentRate;
    private readonly decimal _maxLoanToValuePercent;
    private readonly decimal _maxDebtToIncomePercent;
    private readonly decimal _maxLoanToIncomeMultiple;
    private readonly string _rulesVersion;

    /// <summary>Validates the options and copies their values, so later changes to the object have no effect.</summary>
    /// <exception cref="ArgumentException">The options break a rules section 1 bound.</exception>
    public AffordabilityCalculator(AffordabilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = options.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException($"Invalid affordability options: {string.Join(" ", errors)}", nameof(options));
        }

        _assessmentRate = options.AssessmentRate;
        _maxLoanToValuePercent = options.MaxLoanToValuePercent;
        _maxDebtToIncomePercent = options.MaxDebtToIncomePercent;
        _maxLoanToIncomeMultiple = options.MaxLoanToIncomeMultiple;
        _rulesVersion = options.RulesVersion!;
    }

    /// <exception cref="ArgumentOutOfRangeException">An input breaks a rules section 1 precondition.</exception>
    public AffordabilityResult Assess(AffordabilityInputs inputs)
    {
        InputGuard.EnsureValid(inputs);

        var loan = inputs.PropertyValue - inputs.DepositAmount;
        var loanToValue = loan / inputs.PropertyValue * 100m;

        var monthlyRate = _assessmentRate / 12m;
        var payments = inputs.TermYears * 12;
        var growth = DecimalMath.Pow(1m + monthlyRate, payments);
        var annuity = 1m - (1m / growth);
        var monthlyPayment = loan * monthlyRate / annuity;

        var monthlyIncome = inputs.AnnualIncome / 12m;
        var debtToIncome = (inputs.MonthlyExpenses + monthlyPayment) / monthlyIncome * 100m;
        var loanToIncome = loan / inputs.AnnualIncome;

        var loanToIncomeCap = _maxLoanToIncomeMultiple * inputs.AnnualIncome;
        var affordablePayment = (_maxDebtToIncomePercent / 100m * monthlyIncome) - inputs.MonthlyExpenses;
        var debtToIncomeCap = affordablePayment * annuity / monthlyRate;
        var maxBorrowing = Math.Max(0m, Math.Min(loanToIncomeCap, debtToIncomeCap));

        CheckResult[] checks =
        [
            Check(LoanToValueCheck, loanToValue, _maxLoanToValuePercent),
            Check(DebtToIncomeCheck, debtToIncome, _maxDebtToIncomePercent),
            Check(LoanToIncomeCheck, loanToIncome, _maxLoanToIncomeMultiple),
        ];
        var decision = Array.TrueForAll(checks, c => c.Passed) ? Decision.Approved : Decision.Declined;

        return new AffordabilityResult(
            LoanAmount: DecimalMath.Round2(loan),
            LoanToValuePercent: DecimalMath.Round2(loanToValue),
            MonthlyPayment: DecimalMath.Round2(monthlyPayment),
            DebtToIncomePercent: DecimalMath.Round2(debtToIncome),
            LoanToIncomeMultiple: DecimalMath.Round2(loanToIncome),
            MaxBorrowing: DecimalMath.Round2(maxBorrowing),
            Decision: decision,
            Checks: Array.AsReadOnly(checks),
            Summary: SummaryBuilder.Build(decision, checks),
            AssessmentRate: _assessmentRate,
            RulesVersion: _rulesVersion);
    }

    // Limits are inclusive and compared at full precision (rules section 3, D3); only the outputs are rounded.
    private static CheckResult Check(string name, decimal value, decimal limit) =>
        new(name, DecimalMath.Round2(value), DecimalMath.Round2(limit), value <= limit);
}
