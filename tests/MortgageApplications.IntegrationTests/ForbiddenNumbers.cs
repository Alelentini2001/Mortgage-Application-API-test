using System.Globalization;
using System.Text.RegularExpressions;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// The text forms of an assessment's input and output numbers that must never reach logs, span attributes or metric
/// tags (rules section 6), and a search for them. Forms per value: raw (as stored), trimmed (no trailing zeros), F2,
/// N2 and N0 (invariant), comma decimal ("85000,00"), dot grouping ("85.000,00", "85.000"), and space grouping with
/// U+00A0 and U+202F ("85 000,00"). The app runs with InvariantGlobalization, so the culture forms come from
/// hand-built <see cref="NumberFormatInfo"/>s, not named cultures. A bare integer form (digits only) is searched for
/// only with 5 or more digits: "900" or "30" would match unrelated text. Before the search, GUIDs, W3C trace and span
/// IDs (32 and 16 lowercase hex) and the rules version are removed, so random hex never matches a number by chance.
/// Summary fragments are matched as plain text.
/// </summary>
public sealed partial class ForbiddenNumbers
{
    private static readonly NumberFormatInfo _commaDecimal = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "" };
    private static readonly NumberFormatInfo _dotGrouping = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };
    private static readonly NumberFormatInfo _noBreakSpace = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = " " };
    private static readonly NumberFormatInfo _narrowNoBreakSpace = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = " " };

    private readonly string _rulesVersion;
    private readonly IReadOnlyList<string> _fragments;

    public ForbiddenNumbers(IEnumerable<decimal> values, IEnumerable<string> fragments, string rulesVersion = "1.0")
    {
        Forms = [.. values.SelectMany(FormsOf).Distinct(StringComparer.Ordinal)];
        _fragments = [.. fragments];
        _rulesVersion = rulesVersion;
    }

    /// <summary>Every number form searched for.</summary>
    public IReadOnlyList<string> Forms { get; }

    public static IReadOnlyList<string> FormsOf(decimal value)
    {
        var invariant = CultureInfo.InvariantCulture;
        string[] forms =
        [
            value.ToString(invariant),
            value.ToString("0.############################", invariant),
            value.ToString("F2", invariant),
            value.ToString("N2", invariant),
            value.ToString("N0", invariant),
            value.ToString("F2", _commaDecimal),
            value.ToString("N2", _dotGrouping),
            value.ToString("N0", _dotGrouping),
            value.ToString("N2", _noBreakSpace),
            value.ToString("N0", _noBreakSpace),
            value.ToString("N2", _narrowNoBreakSpace),
            value.ToString("N0", _narrowNoBreakSpace),
        ];
        return [.. forms.Where(f => !BareInteger().IsMatch(f) || f.Length >= 5).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The text with GUIDs, trace and span IDs and the rules version removed.</summary>
    public string Sanitize(string text)
    {
        var withoutIds = HexId().Replace(Guid().Replace(text, " "), " ");
        return Regex.Replace(withoutIds, $@"(?<![\d.]){Regex.Escape(_rulesVersion)}(?![\d.])", " ", RegexOptions.None, TimeSpan.FromSeconds(1));
    }

    /// <summary>Every forbidden form or fragment found in the text, as "form" (empty when clean).</summary>
    public IReadOnlyList<string> Find(string text)
    {
        var clean = Sanitize(text);
        var numbers = Forms.Where(f => Regex.IsMatch(clean, $@"(?<![0-9]){Regex.Escape(f)}(?![0-9])", RegexOptions.None, TimeSpan.FromSeconds(1)));
        var fragments = _fragments.Where(f => clean.Contains(f, StringComparison.Ordinal));
        return [.. numbers.Concat(fragments)];
    }

    /// <summary>Every finding across many texts, as "form in: text" (empty when all are clean).</summary>
    public IReadOnlyList<string> FindIn(IEnumerable<string> texts) =>
        [.. texts.SelectMany(t => Find(t).Select(f => $"'{f}' in: {t}"))];

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex BareInteger();

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex Guid();

    [GeneratedRegex(@"(?<![0-9a-fA-F])(?:[0-9a-f]{32}|[0-9a-f]{16})(?![0-9a-fA-F])")]
    private static partial Regex HexId();
}

/// <summary>Golden examples E1 and E2 (rules section 8): request body, the forbidden numbers, and the loan as F2.</summary>
public sealed record GoldenExample(string Body, ForbiddenNumbers Forbidden, ForbiddenNumbers InputsOnly, string LoanF2)
{
    /// <summary>Summary and Checks text: the exact summaries plus fragments only they contain (≤ U+2264, × U+00D7).</summary>
    private static readonly string[] _fragments = ["checks passed", "is above the", "≤", "×"];

    public static GoldenExample Get(string name) => name switch
    {
        "E1" => Create(
            Http.BaseBody,
            [85_000.00m, 900.00m, 60_000.00m, 300_000.00m],
            [240_000.00m, 80.00m, 1_438.92m, 33.02m, 2.82m, 381_535.82m],
            "Approved: all 3 checks passed (LTV 80.00% ≤ 90.00%, DTI 33.02% ≤ 45.00%, LTI 2.82× ≤ 4.50×).",
            "240000.00"),
        "E2" => Create(
            Http.BodyWith("""{"depositAmount":20000.00}"""),
            [85_000.00m, 900.00m, 20_000.00m, 300_000.00m],
            [280_000.00m, 93.33m, 1_678.74m, 36.41m, 3.29m, 381_535.82m],
            "Declined: LTV 93.33% is above the 90.00% limit.",
            "280000.00"),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Only E1 and E2."),
    };

    private static GoldenExample Create(string body, decimal[] inputs, decimal[] outputs, string summary, string loan) =>
        new(body, new ForbiddenNumbers([.. inputs, .. outputs], [summary, .. _fragments]), new ForbiddenNumbers(inputs, []), loan);
}
