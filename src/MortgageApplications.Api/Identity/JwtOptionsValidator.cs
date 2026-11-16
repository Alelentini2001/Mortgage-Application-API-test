using Microsoft.Extensions.Options;

namespace MortgageApplications.Api.Identity;

/// <summary>
/// Startup checks for the <c>Jwt</c> section (A9, A11). Messages name the setting, never its value. Outside
/// Development a signing public key is required, so the app never runs without a way to validate tokens.
/// </summary>
internal sealed class JwtOptionsValidator(IHostEnvironment environment) : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (options.TokenLifetimeMinutes is < 1 or > 60)
        {
            failures.Add("Jwt:TokenLifetimeMinutes must be between 1 and 60.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningPublicKey))
        {
            if (!environment.IsDevelopment())
            {
                failures.Add("Jwt:SigningPublicKey is required outside Development.");
            }
        }
        else if (!SigningPublicKey.TryRead(options.SigningPublicKey, out var key))
        {
            failures.Add("Jwt:SigningPublicKey must be an ECDSA P-256 public key in PEM format.");
        }
        else
        {
            key.Dispose();
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>
/// Fail closed: the dev token endpoint can never be switched on outside Development, nor together with a configured
/// signing key, so in dev mode the in-memory dev key is the only trusted key (security gate, Phase 7).
/// </summary>
internal sealed class DevAuthOptionsValidator(IHostEnvironment environment, IConfiguration configuration)
    : IValidateOptions<DevAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, DevAuthOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (!environment.IsDevelopment())
        {
            failures.Add("DevAuth:Enabled must be false outside Development.");
        }

        if (!string.IsNullOrWhiteSpace(configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningPublicKey)}"]))
        {
            failures.Add("DevAuth:Enabled must be false when Jwt:SigningPublicKey is set: the dev key must be the only trusted key.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
