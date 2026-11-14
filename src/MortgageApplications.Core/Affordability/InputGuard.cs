namespace MortgageApplications.Core.Affordability;

/// <summary>
/// Throwing form of <see cref="ApplicationInputRules"/>, shared by MortgageApplication.Create and the calculator.
/// Throws ArgumentOutOfRangeException for the first broken rule, naming the field only: the message never
/// contains the value (no ThrowIf* helpers and no actualValue constructor, both of which put the value in the message).
/// </summary>
internal static class InputGuard
{
    public static void EnsureValid(AffordabilityInputs inputs)
    {
        var errors = ApplicationInputRules.Validate(inputs);
        if (errors.Count > 0)
        {
            throw OutOfRange(errors[0].Field, errors[0].Message);
        }
    }

    // ParamName is the input field name (AnnualIncome, ...), not a C# parameter of these methods: the API maps it
    // to the JSON field. Built here so CA2208 doesn't need a suppression. The (paramName, message) constructor
    // never adds the value to the message.
    private static ArgumentOutOfRangeException OutOfRange(string field, string message) => new(field, message);
}
