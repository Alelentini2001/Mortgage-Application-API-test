using System.Globalization;

namespace MortgageApplications.Core.Affordability;

/// <summary>One broken rules section 1 precondition: the input field (PascalCase) and a message without the value.</summary>
public sealed record InputError(string Field, string Message);

/// <summary>
/// The rules section 1 preconditions as one pure rule set. The API maps the errors to a 400 response,
/// and <see cref="InputGuard"/> throws on the first one, so the bounds live in one place only.
/// Messages name the bounds, never the submitted value.
/// </summary>
public static class ApplicationInputRules
{
    public const decimal MinimumLoan = 1_000m;

    /// <summary>Every broken precondition, in field order; empty when the inputs are valid.</summary>
    public static IReadOnlyList<InputError> Validate(AffordabilityInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var errors = new List<InputError>();
        AddMoneyError(errors, inputs.AnnualIncome, 1m, 10_000_000m, nameof(AffordabilityInputs.AnnualIncome));
        AddMoneyError(errors, inputs.MonthlyExpenses, 0m, 1_000_000m, nameof(AffordabilityInputs.MonthlyExpenses));
        var propertyValid = AddMoneyError(
            errors, inputs.PropertyValue, 1_000m, 100_000_000m, nameof(AffordabilityInputs.PropertyValue));

        var depositValid = inputs.DepositAmount >= 0m && DecimalMath.HasAtMostDecimals(inputs.DepositAmount, 2);
        if (!depositValid)
        {
            errors.Add(new(nameof(AffordabilityInputs.DepositAmount), "Must be 0 or more with at most 2 decimal places."));
        }

        if (inputs.TermYears < 5 || inputs.TermYears > 40)
        {
            errors.Add(new(nameof(AffordabilityInputs.TermYears), "Must be between 5 and 40 whole years."));
        }

        // Only meaningful when both amounts are valid on their own; otherwise the client would get two errors
        // for one mistake (Phase 3, A6).
        if (propertyValid && depositValid && inputs.PropertyValue - inputs.DepositAmount < MinimumLoan)
        {
            errors.Add(new(
                nameof(AffordabilityInputs.DepositAmount),
                "Must leave a loan (property value minus deposit) of at least 1,000."));
        }

        return errors;
    }

    private static bool AddMoneyError(List<InputError> errors, decimal value, decimal min, decimal max, string field)
    {
        if (value >= min && value <= max && DecimalMath.HasAtMostDecimals(value, 2))
        {
            return true;
        }

        errors.Add(new(
            field,
            string.Create(CultureInfo.InvariantCulture, $"Must be between {min:#,0} and {max:#,0} with at most 2 decimal places.")));
        return false;
    }
}
