using System.ComponentModel.DataAnnotations;

namespace MortgageApplications.Api.Security;

/// <summary>The <c>RateLimiting</c> configuration section, validated at startup (P8-D1 A).</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>How many write requests a subject may make in one fixed window before receiving 429.</summary>
    [Range(1, 10_000)]
    public int PermitLimit { get; set; } = 60;

    /// <summary>The length of the fixed window in seconds.</summary>
    [Range(1, 3_600)]
    public int WindowSeconds { get; set; } = 60;
}
