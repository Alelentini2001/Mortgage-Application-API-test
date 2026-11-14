namespace MortgageApplications.Core.Affordability;

/// <summary>One broken rules section 1 precondition: the input field (PascalCase) and a message without the value.</summary>
public sealed record InputError(string Field, string Message);

/// <summary>
/// The rules section 1 preconditions as one pure rule set. The API maps the errors to a 400 response,
/// and <see cref="InputGuard"/> throws on the first one, so the bounds live in one place only.
/// </summary>
public static class ApplicationInputRules
{
    public const decimal MinimumLoan = 1_000m;

    /// <summary>Every broken precondition, in field order; empty when the inputs are valid.</summary>
    public static IReadOnlyList<InputError> Validate(AffordabilityInputs inputs) => throw new NotImplementedException();
}
