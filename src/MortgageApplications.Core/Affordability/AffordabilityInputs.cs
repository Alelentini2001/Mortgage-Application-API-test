namespace MortgageApplications.Core.Affordability;

/// <summary>The five application fields the calculator reads (rules section 1).</summary>
/// <remarks>ToString is redacted: these are personal financial data and must never reach a log (rules section 6).</remarks>
public sealed record AffordabilityInputs(
    decimal AnnualIncome,
    decimal MonthlyExpenses,
    decimal DepositAmount,
    decimal PropertyValue,
    int TermYears)
{
    public override string ToString() => $"{nameof(AffordabilityInputs)} {{ redacted }}";
}
