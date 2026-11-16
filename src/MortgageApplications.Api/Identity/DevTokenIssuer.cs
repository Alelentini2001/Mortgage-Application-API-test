using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MortgageApplications.Api.Identity;

/// <summary>Signs dev tokens (ES256) with the in-memory key pair from <see cref="TokenKeys"/>.</summary>
internal sealed class DevTokenIssuer(TokenKeys keys, IOptions<JwtOptions> jwt, TimeProvider time)
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    public DevTokenResponse Issue(string subject)
    {
        var key = keys.DevSigningKey
            ?? throw new InvalidOperationException("The dev token endpoint is mapped without a dev signing key.");
        // Whole seconds, so expiresAt is exactly the token's exp claim.
        var now = DateTimeOffset.FromUnixTimeSeconds(time.GetUtcNow().ToUnixTimeSeconds());
        var expires = now.AddMinutes(jwt.Value.TokenLifetimeMinutes);
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Value.Issuer,
            Audience = jwt.Value.Audience,
            Claims = new Dictionary<string, object>
            {
                [SubjectClaim.Type] = subject,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256),
        });
        return new DevTokenResponse(token, expires);
    }
}
