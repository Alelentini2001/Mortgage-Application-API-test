using System.Globalization;

namespace MortgageApplications.Core.Affordability;

/// <summary>
/// Rules section 1 preconditions, shared by MortgageApplication.Create and the calculator.
/// Throws ArgumentOutOfRangeException naming the field only: the message never contains the value
/// (no ThrowIf* helpers and no actualValue constructor, both of which put the value in the message).
/// </summary>
internal static class InputGuard
{
    public const decimal MinimumLoan = 1_000m;

    public static void EnsureValid(AffordabilityInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        EnsureMoney(inputs.AnnualIncome, 1m, 10_000_000m, nameof(AffordabilityInputs.AnnualIncome));
        EnsureMoney(inputs.MonthlyExpenses, 0m, 1_000_000m, nameof(AffordabilityInputs.MonthlyExpenses));
        EnsureMoney(inputs.PropertyValue, 1_000m, 100_000_000m, nameof(AffordabilityInputs.PropertyValue));

        if (inputs.DepositAmount < 0m || !DecimalMath.HasAtMostDecimals(inputs.DepositAmount, 2))
        {
            throw OutOfRange(
                nameof(AffordabilityInputs.DepositAmount),
                "Must be 0 or more with at most 2 decimal places.");
        }

        if (inputs.TermYears < 5 || inputs.TermYears > 40)
        {
            throw OutOfRange(
                nameof(AffordabilityInputs.TermYears),
                "Must be between 5 and 40 whole years.");
        }

        if (inputs.PropertyValue - inputs.DepositAmount < MinimumLoan)
        {
            throw OutOfRange(
                nameof(AffordabilityInputs.DepositAmount),
                "Must leave a loan (property value minus deposit) of at least 1,000.");
        }
    }

    private static void EnsureMoney(decimal value, decimal min, decimal max, string name)
    {
        if (value < min || value > max || !DecimalMath.HasAtMostDecimals(value, 2))
        {
            throw OutOfRange(
                name,
                string.Create(CultureInfo.InvariantCulture, $"Must be between {min:#,0} and {max:#,0} with at most 2 decimal places."));
        }
    }

    // ParamName is the input field name (AnnualIncome, ...), not a C# parameter of these methods: callers map it
    // to the API field in Phase 3. Built here so CA2208 doesn't need a suppression. The (paramName, message)
    // constructor never adds the value to the message.
    private static ArgumentOutOfRangeException OutOfRange(string field, string message) => new(field, message);
}
