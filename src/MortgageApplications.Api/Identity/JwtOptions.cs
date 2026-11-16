namespace MortgageApplications.Api.Identity;

/// <summary>The <c>Jwt</c> configuration section, validated at startup (<see cref="JwtOptionsValidator"/>).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>The only accepted <c>iss</c>, and the issuer of dev tokens.</summary>
    public string Issuer { get; set; } = "";

    /// <summary>The only accepted <c>aud</c>, and the audience of dev tokens.</summary>
    public string Audience { get; set; } = "";

    /// <summary>Lifetime of a dev token, 1 to 60 minutes (A11).</summary>
    public int TokenLifetimeMinutes { get; set; } = 15;

    /// <summary>
    /// The ECDSA P-256 public key (SubjectPublicKeyInfo PEM) that signs accepted tokens. Required outside
    /// Development; in Development without it, only dev tokens (when <c>DevAuth:Enabled</c>) are accepted.
    /// </summary>
    public string? SigningPublicKey { get; set; }
}
