using MortgageApplications.Core.Affordability;

namespace MortgageApplications.UnitTests.Affordability;

/// <summary>Rules section 1, option bounds (O1–O3).</summary>
public sealed class AffordabilityOptionsTests
{
    public static TheoryData<string, decimal> InvalidValues => new()
    {
        { nameof(AffordabilityOptions.AssessmentRate), 0m },
        { nameof(AffordabilityOptions.AssessmentRate), -0.06m },
        { nameof(AffordabilityOptions.AssessmentRate), 0.0009m },
        { nameof(AffordabilityOptions.AssessmentRate), 0.2501m },
        { nameof(AffordabilityOptions.AssessmentRate), 0.06001m },
        { nameof(AffordabilityOptions.MaxLoanToValuePercent), 0m },
        { nameof(AffordabilityOptions.MaxLoanToValuePercent), -1m },
        { nameof(AffordabilityOptions.MaxLoanToValuePercent), 100.01m },
        { nameof(AffordabilityOptions.MaxLoanToValuePercent), 90.005m },
        { nameof(AffordabilityOptions.MaxDebtToIncomePercent), 0m },
        { nameof(AffordabilityOptions.MaxDebtToIncomePercent), 100.01m },
        { nameof(AffordabilityOptions.MaxDebtToIncomePercent), 45.005m },
        { nameof(AffordabilityOptions.MaxLoanToIncomeMultiple), 0m },
        { nameof(AffordabilityOptions.MaxLoanToIncomeMultiple), 10.01m },
        { nameof(AffordabilityOptions.MaxLoanToIncomeMultiple), 4.505m },
    };

    public static TheoryData<string, decimal> ValidValues => new()
    {
        { nameof(AffordabilityOptions.AssessmentRate), 0.001m },
        { nameof(AffordabilityOptions.AssessmentRate), 0.25m },
        { nameof(AffordabilityOptions.AssessmentRate), 0.0505m },
        { nameof(AffordabilityOptions.MaxLoanToValuePercent), 0.01m },
        { nameof(AffordabilityOptions.MaxLoanToValuePercent), 100m },
        { nameof(AffordabilityOptions.MaxDebtToIncomePercent), 0.01m },
        { nameof(AffordabilityOptions.MaxDebtToIncomePercent), 100m },
        { nameof(AffordabilityOptions.MaxLoanToIncomeMultiple), 0.01m },
        { nameof(AffordabilityOptions.MaxLoanToIncomeMultiple), 10m },
    };

    public static TheoryData<string> BlankRulesVersions => ["", "   "];

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public void Validate_value_out_of_bounds_returns_one_error_naming_the_option(string option, decimal value)
    {
        var options = With(option, value);

        var errors = options.Validate();

        var error = Assert.Single(errors);
        Assert.Contains(option, error, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ValidValues))]
    public void Validate_value_at_bound_returns_no_errors(string option, decimal value)
    {
        Assert.Empty(With(option, value).Validate());
    }

    [Theory]
    [MemberData(nameof(BlankRulesVersions))]
    public void Validate_blank_rules_version_returns_one_error(string version)
    {
        var options = TestData.Rules10();
        options.RulesVersion = version;

        var error = Assert.Single(options.Validate());

        Assert.Contains(nameof(AffordabilityOptions.RulesVersion), error, StringComparison.Ordinal);
    }

    // U17: RulesVersion must fit its 16-character column (Phase 3).
    [Fact]
    public void Validate_rules_version_of_16_characters_returns_no_errors()
    {
        var options = TestData.Rules10();
        options.RulesVersion = new string('v', 16);

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Validate_rules_version_of_17_characters_returns_one_error_without_the_value()
    {
        var options = TestData.Rules10();
        options.RulesVersion = "SENTINEL-17-CHARS";

        var error = Assert.Single(options.Validate());

        Assert.Contains(nameof(AffordabilityOptions.RulesVersion), error, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_null_rules_version_returns_one_error()
    {
        var options = TestData.Rules10();
        options.RulesVersion = null;

        var error = Assert.Single(options.Validate());

        Assert.Contains(nameof(AffordabilityOptions.RulesVersion), error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_errors_do_not_contain_the_configured_values()
    {
        var options = new AffordabilityOptions
        {
            AssessmentRate = 0.98765m,
            MaxLoanToValuePercent = 123.45m,
            MaxDebtToIncomePercent = 678.91m,
            MaxLoanToIncomeMultiple = 23.45m,
            RulesVersion = "1.0",
        };

        var errors = string.Join(" ", options.Validate());

        Assert.DoesNotContain("98765", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("123.45", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("678.91", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("23.45", errors, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rules_10_returns_no_errors()
    {
        Assert.Empty(TestData.Rules10().Validate());
    }

    [Fact]
    public void Validate_every_option_invalid_reports_all_five()
    {
        var options = new AffordabilityOptions();

        var errors = options.Validate();

        Assert.Equal(5, errors.Count);
    }

    private static AffordabilityOptions With(string option, decimal value)
    {
        var options = TestData.Rules10();
        switch (option)
        {
            case nameof(AffordabilityOptions.AssessmentRate): options.AssessmentRate = value; break;
            case nameof(AffordabilityOptions.MaxLoanToValuePercent): options.MaxLoanToValuePercent = value; break;
            case nameof(AffordabilityOptions.MaxDebtToIncomePercent): options.MaxDebtToIncomePercent = value; break;
            case nameof(AffordabilityOptions.MaxLoanToIncomeMultiple): options.MaxLoanToIncomeMultiple = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(option));
        }

        return options;
    }
}
