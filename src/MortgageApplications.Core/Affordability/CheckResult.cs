namespace MortgageApplications.Core.Affordability;

/// <summary>One affordability check: its name (LTV, DTI or LTI), rounded value, configured limit and outcome.</summary>
/// <remarks>ToString leaves out Value: derived values reveal the applicant's inputs (rules section 6).</remarks>
public sealed record CheckResult(string Name, decimal Value, decimal Limit, bool Passed)
{
    public override string ToString() => $"{nameof(CheckResult)} {{ Name = {Name}, Passed = {Passed} }}";
}
