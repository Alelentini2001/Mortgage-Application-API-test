using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MortgageApplications.Core.Affordability;

namespace MortgageApplications.Infrastructure.Persistence;

/// <summary>
/// Stores an <see cref="AffordabilityResult"/> as one JSON text column (P3-D3, fallback b). EF 10 complex types
/// couldn't be used: a JSON collection inside a column-split complex type isn't supported (efcore #36558), and a
/// complex collection must be declared as <c>IList&lt;T&gt;</c>, which would make the domain's checks mutable.
/// System.Text.Json keeps the decimal scale (240000.00 stays 240000.00) and the decision is stored by name.
/// </summary>
internal static class AffordabilityResultJson
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.General)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static ValueConverter<AffordabilityResult?, string> Converter { get; } = new(
        result => Serialize(result),
        json => Deserialize(json));

    /// <summary>The result is immutable, so two results are equal when their JSON is equal, and the snapshot is the same instance.</summary>
    public static ValueComparer<AffordabilityResult?> Comparer { get; } = new(
        (left, right) => Serialize(left) == Serialize(right),
        result => Serialize(result).GetHashCode(StringComparison.Ordinal),
        result => result);

    private static string Serialize(AffordabilityResult? result) => JsonSerializer.Serialize(result, _options);

    // The serializer builds Checks as a List; wrap a copy read-only, as AffordabilityAssessment.Complete does, so the
    // comparer's "same instance" snapshot stays valid for tracked loads (review F3).
    private static AffordabilityResult? Deserialize(string json)
    {
        var result = JsonSerializer.Deserialize<AffordabilityResult>(json, _options)
            ?? throw new InvalidOperationException("A stored assessment result is empty.");
        return result with { Checks = Array.AsReadOnly(result.Checks.ToArray()) };
    }
}
