using System.Text.Json;

namespace MortgageApplications.Api.Errors;

/// <summary>
/// In .NET 10, <c>AddValidation</c> names attribute errors by the C# property (<c>TermYears</c>), while clients send
/// camelCase JSON (<c>termYears</c>). This renames every <c>errors</c> key to its JSON name, so a 400 always points
/// at the property the client sent (Phase 3, ambiguity A1).
/// </summary>
internal static class ValidationErrorKeys
{
    public static void UseJsonNames(ProblemDetailsContext context)
    {
        if (context.ProblemDetails is not HttpValidationProblemDetails validation || validation.Errors.Count == 0)
        {
            return;
        }

        var renamed = validation.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.Key), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.SelectMany(e => e.Value).ToArray(), StringComparer.Ordinal);
        validation.Errors.Clear();
        foreach (var (key, messages) in renamed)
        {
            validation.Errors[key] = messages;
        }
    }
}
