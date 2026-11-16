using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MortgageApplications.Api.Identity;

/// <summary>
/// The keys that validate tokens (P7-D1 A, ES256 only). Exactly one source: when the dev token endpoint is on
/// (Development with <c>DevAuth:Enabled</c>) an ECDSA P-256 key pair generated in memory at startup, and otherwise the
/// configured <c>Jwt:SigningPublicKey</c>, if any. Startup fails if both are set (<see cref="DevAuthOptionsValidator"/>).
/// The dev private key never leaves the process and dies with it, so a dev token works only on the instance that
/// issued it. With no key at all (Development, no key, DevAuth off) every token is rejected (A10).
/// <para>
/// Each key gets its own <see cref="CryptoProviderFactory"/> without the signature-provider cache. The default factory
/// is static and caches providers by the key's thumbprint, so a second host with the same public key (as in the
/// integration tests) would reuse a provider wrapping the first host's key after this class disposed it, and reject
/// valid tokens.
/// </para>
/// </summary>
internal sealed class TokenKeys : IDisposable
{
    private readonly List<ECDsa> _owned = [];

    public TokenKeys(IOptions<JwtOptions> jwt, IOptions<DevAuthOptions> devAuth, IHostEnvironment environment)
    {
        var keys = new List<SecurityKey>();
        if (environment.IsDevelopment() && devAuth.Value.Enabled)
        {
            var devKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _owned.Add(devKey);
            DevSigningKey = Uncached(new ECDsaSecurityKey(devKey));
            keys.Add(DevSigningKey);
        }
        else if (!string.IsNullOrWhiteSpace(jwt.Value.SigningPublicKey)
            && SigningPublicKey.TryRead(jwt.Value.SigningPublicKey, out var configured))
        {
            _owned.Add(configured);
            keys.Add(Uncached(new ECDsaSecurityKey(configured)));
        }

        ValidationKeys = keys;
    }

    public IReadOnlyList<SecurityKey> ValidationKeys { get; }

    /// <summary>The in-memory dev key pair; null unless the dev token endpoint is on.</summary>
    public ECDsaSecurityKey? DevSigningKey { get; }

    private static ECDsaSecurityKey Uncached(ECDsaSecurityKey key)
    {
        key.CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false };
        return key;
    }

    public void Dispose()
    {
        foreach (var key in _owned)
        {
            key.Dispose();
        }
    }
}
