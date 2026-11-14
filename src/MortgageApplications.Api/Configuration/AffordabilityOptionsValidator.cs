using Microsoft.Extensions.Options;
using MortgageApplications.Core.Affordability;

namespace MortgageApplications.Api.Configuration;

/// <summary>Runs the Core options rules at startup (<c>ValidateOnStart</c>, security S4). Messages never contain values.</summary>
internal sealed class AffordabilityOptionsValidator : IValidateOptions<AffordabilityOptions>
{
    public ValidateOptionsResult Validate(string? name, AffordabilityOptions options)
    {
        var errors = options.Validate();
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
