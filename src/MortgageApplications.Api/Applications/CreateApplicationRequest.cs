using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MortgageApplications.Core.Affordability;

namespace MortgageApplications.Api.Applications;

/// <summary>
/// The five application fields (spec requirement 1). Properties are nullable so a missing field is a 400, not a
/// silent 0. <c>AddValidation</c> checks the attributes first; <see cref="Validate"/> (the Core rule set, so the
/// bounds live in one place) runs only when they all pass. There is deliberately no owner field: the owner comes
/// from the current user, and unknown JSON properties are rejected.
/// </summary>
public sealed record CreateApplicationRequest : IValidatableObject
{
    /// <summary>Gross annual income, 1 to 10,000,000, at most 2 decimal places.</summary>
    [Required(ErrorMessage = RequiredMessage)]
    public decimal? AnnualIncome { get; init; }

    /// <summary>Existing monthly commitments (excluding the new mortgage), 0 to 1,000,000, at most 2 decimal places.</summary>
    [Required(ErrorMessage = RequiredMessage)]
    public decimal? MonthlyExpenses { get; init; }

    /// <summary>0 or more, at most 2 decimal places, leaving a loan of at least 1,000.</summary>
    [Required(ErrorMessage = RequiredMessage)]
    public decimal? DepositAmount { get; init; }

    /// <summary>1,000 to 100,000,000, at most 2 decimal places.</summary>
    [Required(ErrorMessage = RequiredMessage)]
    public decimal? PropertyValue { get; init; }

    /// <summary>Whole years, 5 to 40.</summary>
    [Required(ErrorMessage = RequiredMessage)]
    [Range(5, 40, ErrorMessage = "Must be between 5 and 40 whole years.")]
    public int? TermYears { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        ApplicationInputRules.Validate(ToInputs())
            .Select(e => new ValidationResult(e.Message, [JsonNamingPolicy.CamelCase.ConvertName(e.Field)]));

    /// <summary>Call only after validation: every property is then set.</summary>
    public AffordabilityInputs ToInputs() => new(
        AnnualIncome.GetValueOrDefault(),
        MonthlyExpenses.GetValueOrDefault(),
        DepositAmount.GetValueOrDefault(),
        PropertyValue.GetValueOrDefault(),
        TermYears.GetValueOrDefault());

    /// <summary>Personal financial data must never reach a log (rules section 6, security L1).</summary>
    public override string ToString() => $"{nameof(CreateApplicationRequest)} {{ redacted }}";

    // Same wording style as the Core rules; the default messages name the C# property (TermYears).
    private const string RequiredMessage = "Required.";
}
