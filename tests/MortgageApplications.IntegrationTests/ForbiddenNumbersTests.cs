namespace MortgageApplications.IntegrationTests;

/// <summary>Self-test of the Phase 5 forbidden-number search, so a clean scan means something.</summary>
public sealed class ForbiddenNumbersTests
{
    private static readonly string[] _formsOf85000 =
        ["85000.00", "85000", "85,000.00", "85,000", "85000,00", "85.000,00", "85.000", "85\u00A0000,00", "85\u00A0000", "85\u202F000,00", "85\u202F000", "85000.0", "85000,0", "85000.0000", "85000,0000"];

    private static readonly ForbiddenNumbers _search = new([85_000.00m, 900.00m, 1_438.92m, 2.82m], ["is above the"]);

    // Every listed form of 85,000.00 is produced.
    [Fact]
    public void FormsOf_produces_every_listed_form()
    {
        var forms = ForbiddenNumbers.FormsOf(85_000.00m);

        Assert.Equivalent(_formsOf85000, forms, strict: true);
    }

    // Bare integers below 5 digits are not searched; their decimal forms are.
    [Fact]
    public void FormsOf_skips_short_bare_integers()
    {
        var forms = ForbiddenNumbers.FormsOf(900.00m);

        Assert.DoesNotContain("900", forms);
        Assert.Contains("900.00", forms);
        Assert.Contains("900,00", forms);
    }

    public static TheoryData<string, string> Leaks => new()
    {
        { "loan 85,000.00 here", "85,000.00" },
        { "x=85000", "85000" },
        { "payment 1.438,92", "1.438,92" },
        { "payment 1 438,92", "1 438,92" },
        { "LTI 2.82", "2.82" },
        { "LTI 2.825", "2.82" },
        { "LTV 93.33% is above the 90.00% limit", "is above the" },
    };

    // Each form is found where it really is.
    [Theory]
    [MemberData(nameof(Leaks))]
    public void Find_reports_a_leaked_form(string text, string form)
    {
        Assert.Contains(form, _search.Find(text));
    }

    public static TheoryData<string> CleanTexts => new()
    {
        "id 1850001 and 12.82",
        "application a1b2c3d4-0000-4000-8000-a85000b00000 done",
        "trace 4bf92f3577b34daa85000aa0e0e47360 span 00f067aa0ba85000",
        "900 a month over 30 years",
    };

    // Longer numbers, GUIDs, trace and span IDs and short bare integers never match.
    [Theory]
    [MemberData(nameof(CleanTexts))]
    public void Find_ignores_ids_longer_numbers_and_short_integers(string text)
    {
        Assert.Empty(_search.Find(text));
    }

    public static TheoryData<string> UnroundedLeaks => new()
    {
        "ltv=0.8",
        "dti=0.3302",
        "lti=2.8235",
        "ltv 80.0%",
        "payment 1438.9",
    };

    // Review gate: E1's unrounded values, fractions and 1 dp forms are forbidden too (prefix match).
    [Theory]
    [MemberData(nameof(UnroundedLeaks))]
    public void Golden_E1_forbids_unrounded_and_fraction_forms(string text)
    {
        Assert.NotEmpty(GoldenExample.Get("E1").Forbidden.Find(text));
    }
}
