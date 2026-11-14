namespace MortgageApplications.Core.Affordability;

/// <summary>One affordability check: its name (LTV, DTI or LTI), rounded value, configured limit and outcome.</summary>
public sealed record CheckResult(string Name, decimal Value, decimal Limit, bool Passed);
