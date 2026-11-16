using System.Buffers.Text;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MortgageApplications.IntegrationTests;

/// <summary>The claims and times of a test token. A null value leaves that claim out.</summary>
public sealed record TokenSpec
{
    public string? Issuer { get; init; } = ApiFactory.Issuer;

    public string? Audience { get; init; } = ApiFactory.Audience;

    public string? Subject { get; init; } = ApiFactory.DefaultOwner;

    public DateTime NotBefore { get; init; }

    public DateTime Expires { get; init; }

    /// <summary>Valid from now for 15 minutes (the app's default lifetime).</summary>
    public static TokenSpec Valid(string? subject = ApiFactory.DefaultOwner)
    {
        var now = DateTime.UtcNow;
        return new TokenSpec { Subject = subject, NotBefore = now, Expires = now.AddMinutes(15) };
    }

    /// <summary>Expired <paramref name="ago"/> before now (issued 16 minutes ago).</summary>
    public static TokenSpec ExpiredAgo(TimeSpan ago, string? subject = ApiFactory.DefaultOwner)
    {
        var now = DateTime.UtcNow;
        return new TokenSpec { Subject = subject, NotBefore = now.AddMinutes(-16), Expires = now - ago };
    }
}

/// <summary>
/// Test tokens without new packages: <see cref="JsonWebTokenHandler"/> (it comes with the JwtBearer package through
/// the Api project) for well-formed ES256 tokens, and hand-built <c>header.payload.signature</c> text for the
/// malformed ones (alg none, HS256 confusion).
/// </summary>
internal static class TestTokens
{
    public static string Sign(TokenSpec spec, ECDsa key)
    {
        var claims = new Dictionary<string, object>();
        if (spec.Subject is not null)
        {
            claims["sub"] = spec.Subject;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = spec.Issuer,
            Audience = spec.Audience,
            Claims = claims,
            IssuedAt = spec.NotBefore,
            NotBefore = spec.NotBefore,
            Expires = spec.Expires,
            SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(key), SecurityAlgorithms.EcdsaSha256),
        };
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    /// <summary>A token with any header, the spec's claims and a signature computed by <paramref name="sign"/>.</summary>
    public static string Raw(object header, TokenSpec spec, Func<byte[], string> sign) => Raw(header, Payload(spec), sign);

    /// <summary>A token with any header, any payload and a signature computed by <paramref name="sign"/>.</summary>
    public static string Raw(object header, IDictionary<string, object> payload, Func<byte[], string> sign)
    {
        var signingInput = $"{Encode(JsonSerializer.SerializeToUtf8Bytes(header))}.{Encode(JsonSerializer.SerializeToUtf8Bytes(payload))}";
        return $"{signingInput}.{sign(Encoding.ASCII.GetBytes(signingInput))}";
    }

    /// <summary>A hand-built token really signed with ES256 by <paramref name="key"/>, whatever the header says.</summary>
    public static string Es256(object header, IDictionary<string, object> payload, ECDsa key) =>
        Raw(header, payload, input => Encode(key.SignData(input, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));

    /// <summary>The spec's claims as a JSON payload: iat, nbf, exp, and iss, aud and sub when set.</summary>
    public static Dictionary<string, object> Payload(TokenSpec spec)
    {
        var payload = new Dictionary<string, object>
        {
            ["iat"] = new DateTimeOffset(spec.NotBefore).ToUnixTimeSeconds(),
            ["nbf"] = new DateTimeOffset(spec.NotBefore).ToUnixTimeSeconds(),
            ["exp"] = new DateTimeOffset(spec.Expires).ToUnixTimeSeconds(),
        };
        AddIfSet(payload, "iss", spec.Issuer);
        AddIfSet(payload, "aud", spec.Audience);
        AddIfSet(payload, "sub", spec.Subject);
        return payload;
    }

    /// <summary>The public half of a P-256 key as a JWK (for a <c>jwk</c> header).</summary>
    public static object Jwk(ECDsa key)
    {
        var parameters = key.ExportParameters(includePrivateParameters: false);
        return new { kty = "EC", crv = "P-256", x = Encode(parameters.Q.X!), y = Encode(parameters.Q.Y!) };
    }

    /// <summary>Unsigned: <c>{"alg":"none"}</c> and an empty signature.</summary>
    public static string AlgNone(TokenSpec spec) => Raw(new { alg = "none", typ = "JWT" }, spec, _ => "");

    /// <summary>Algorithm confusion: HS256 with the public key's bytes as the HMAC secret.</summary>
    public static string Hs256(TokenSpec spec, byte[] secret) =>
        Raw(new { alg = "HS256", typ = "JWT" }, spec, input => Encode(HMACSHA256.HashData(secret, input)));

    /// <summary>
    /// Changes one character in the middle of the signature. The last character is not used: it can carry unused
    /// base64url bits, so changing it may leave the decoded signature the same.
    /// </summary>
    public static string TamperSignature(string token)
    {
        var dot = token.LastIndexOf('.');
        var middle = dot + ((token.Length - dot) / 2);
        var replacement = token[middle] == 'A' ? 'B' : 'A';
        return string.Concat(token.AsSpan(0, middle), replacement.ToString(), token.AsSpan(middle + 1));
    }

    public static string Encode(byte[] bytes) => Base64Url.EncodeToString(bytes);

    private static void AddIfSet(Dictionary<string, object> payload, string name, string? value)
    {
        if (value is not null)
        {
            payload[name] = value;
        }
    }
}

/// <summary>Switches a client's default Authorization header.</summary>
internal static class AuthHttp
{
    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static HttpClient WithoutToken(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }

    /// <summary>A POST with its own Authorization header value (sent as is, so malformed values reach the server).</summary>
    public static Task<HttpResponseMessage> PostJsonWithAuthorizationAsync(
        this HttpClient client, string url, string json, string authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url, UriKind.Relative))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        return client.SendAsync(request, Http.Ct);
    }
}
