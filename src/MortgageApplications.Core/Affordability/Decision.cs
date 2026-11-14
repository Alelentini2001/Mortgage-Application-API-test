namespace MortgageApplications.Core.Affordability;

/// <summary>Outcome of an affordability assessment. Declined is a valid result, not an error.</summary>
public enum Decision
{
    Approved,
    Declined,
}
