using MortgageApplications.Core.Affordability;

namespace MortgageApplications.Core.Applications;

/// <summary>
/// A submitted mortgage application. Immutable after creation; built only through <see cref="Create"/>,
/// which enforces the rules section 1 preconditions with the same guard as the calculator.
/// </summary>
public sealed class MortgageApplication
{
    private MortgageApplication(Guid id, string ownerId, AffordabilityInputs inputs, DateTimeOffset createdAt)
    {
        Id = id;
        OwnerId = ownerId;
        // The guard allows at most 2 dp by value, so Round2 never changes the value; it only normalises the scale
        // (100.500 → 100.50), so the stored and returned values always show 2 dp.
        AnnualIncome = DecimalMath.Round2(inputs.AnnualIncome);
        MonthlyExpenses = DecimalMath.Round2(inputs.MonthlyExpenses);
        DepositAmount = DecimalMath.Round2(inputs.DepositAmount);
        PropertyValue = DecimalMath.Round2(inputs.PropertyValue);
        TermYears = inputs.TermYears;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    /// <summary>The caller's identity (from the token's <c>sub</c> claim from Phase 7). Never taken from a request body.</summary>
    public string OwnerId { get; }

    public decimal AnnualIncome { get; }

    public decimal MonthlyExpenses { get; }

    public decimal DepositAmount { get; }

    public decimal PropertyValue { get; }

    public int TermYears { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <exception cref="ArgumentException">The owner is blank or the time is not UTC.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An input breaks a rules section 1 precondition.</exception>
    public static MortgageApplication Create(string ownerId, AffordabilityInputs inputs, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        InputGuard.EnsureValid(inputs);
        Timestamps.EnsureUtc(createdAt, nameof(createdAt));

        // Version 7: time-ordered, so new rows append to the primary-key index (Phase 3).
        return new MortgageApplication(Guid.CreateVersion7(createdAt), ownerId, inputs, createdAt);
    }

    public AffordabilityInputs ToInputs() => new(AnnualIncome, MonthlyExpenses, DepositAmount, PropertyValue, TermYears);
}
