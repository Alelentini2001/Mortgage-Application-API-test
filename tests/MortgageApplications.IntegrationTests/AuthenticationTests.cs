using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 7 authentication (AU01–AU32): a valid ES256 token is accepted and its <c>sub</c> is the owner; every other
/// token is a 401 ProblemDetails with <c>WWW-Authenticate: Bearer</c>, creates nothing and leaks nothing.
/// </summary>
public sealed class AuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string _sentinelOwner = "sentinel-owner-7";

    // AU01: a valid token's sub is the owner of what it creates.
    [Fact]
    public async Task Post_with_valid_token_creates_the_application_owned_by_the_sub()
    {
        var client = factory.CreateClient().WithToken(factory.TokenFor("alice"));

        var response = await client.PostJsonAsync("/api/applications", Http.BaseBody);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = await response.IdAsync();
        Assert.Equal($"/api/applications/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal("alice", await OwnerOfAsync(id));
    }

    // AU02: the owner's own application and assessment work end to end with the token.
    [Fact]
    public async Task Owner_with_token_gets_application_and_assessment()
    {
        var client = factory.CreateClient().WithToken(factory.TokenFor("alice"));
        var id = await client.CreateApplicationAsync();

        var posted = await client.PostAssessmentAsync(id);
        var application = await client.GetPathAsync($"/api/applications/{id}");
        var assessment = await client.GetPathAsync(posted.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.Accepted, posted.StatusCode);
        Assert.StartsWith($"/api/applications/{id}/assessments/", posted.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, application.StatusCode);
        Assert.Equal(HttpStatusCode.OK, assessment.StatusCode);
    }

    // AU03: /health needs no token.
    [Fact]
    public async Task Health_without_token_returns_200()
    {
        var response = await factory.CreateAnonymousClient().GetPathAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // AU04: a 128-character sub (the owner column size) is accepted and stored whole.
    [Fact]
    public async Task Post_with_128_character_sub_creates_the_application()
    {
        var subject = new string('a', 128);
        var client = factory.CreateClient().WithToken(factory.TokenFor(subject));

        var response = await client.PostJsonAsync("/api/applications", Http.BaseBody);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(subject, await OwnerOfAsync(await response.IdAsync()));
    }

    // AU06: the background worker has no user and still completes the assessment.
    [Fact]
    public async Task Worker_completes_an_assessment_started_with_a_token()
    {
        var client = factory.CreateClient().WithToken(factory.TokenFor("alice"));
        var posted = await client.PostAssessmentAsync(await client.CreateApplicationAsync());

        var body = await client.PollUntilFinishedAsync(posted.Headers.Location!.OriginalString);

        Assert.Equal("Completed", Http.Status(body));
    }

    // AU10: no token is a 401 on create and on an unknown ID (authentication runs before the 404), and creates nothing.
    [Fact]
    public async Task Request_without_token_returns_401_problem_and_creates_nothing()
    {
        var client = factory.CreateAnonymousClient();
        var before = await CountApplicationsAsync();

        var post = await client.PostJsonAsync("/api/applications", Http.BaseBody);
        var get = await client.GetPathAsync($"/api/applications/{Guid.NewGuid()}");

        foreach (var response in new[] { post, get })
        {
            await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
            Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).ToString());
        }

        Assert.Equal(before, await CountApplicationsAsync());
    }

    // AU11, AU13–AU15, AU18, AU20, AU21, AU23, AU24, AU26–AU29, and the gate's G4 (no exp, header alg, kid, jwk, jku)
    // and G5 (non-string or repeated sub) cases (AU31 and AU32 checked on each): every bad token is a
    // 401 ProblemDetails (status, title, traceId) with exactly `Bearer error="invalid_token"` (no error_description,
    // A5), creates nothing, and neither the body nor any header holds the token, a token segment, IdentityModel text
    // (IDX codes) or the sentinel subject.
    [Theory]
    [MemberData(nameof(BadTokens))]
    public async Task Post_with_bad_token_returns_401_problem_and_leaks_nothing(string badToken)
    {
        var token = BadToken(badToken);
        var before = await CountApplicationsAsync();

        var response = await factory.CreateAnonymousClient()
            .PostJsonWithAuthorizationAsync("/api/applications", Http.BaseBody, $"Bearer {token}");

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("Bearer error=\"invalid_token\"", Assert.Single(response.Headers.WwwAuthenticate).ToString());
        Assert.Equal(before, await CountApplicationsAsync());
        var visible = await response.BodyAsync() + string.Join(
            "\n", response.Headers.Concat(response.Content.Headers).Select(h => $"{h.Key}: {string.Join(",", h.Value)}"));
        var forbidden = token.Split('.').Where(segment => segment.Length >= 8)
            .Append(token)
            .Append(_sentinelOwner)
            .Append("IDX")
            .Append("IdentityModel");
        foreach (var text in forbidden)
        {
            Assert.DoesNotContain(text, visible, StringComparison.Ordinal);
        }
    }

    public static TheoryData<string> BadTokens =>
    [
        "AU11 malformed",
        "AU13 tampered signature",
        "AU14 signed with another key",
        "AU15 expired 45 s ago (past the 30 s skew)",
        "AU18 not valid for 5 minutes",
        "AU20 wrong issuer",
        "AU21 wrong audience",
        "AU23 alg none",
        "AU24 HS256 with the public key DER as secret",
        "AU24 HS256 with the public key PEM as secret",
        "AU26 no sub",
        "AU27 empty sub",
        "AU28 whitespace-only sub",
        "AU29 129-character sub",
        "G4 no exp claim",
        "G4 ES256 signature under an ES384 header",
        "G4 another key with an unknown kid",
        "G4 another key with its own jwk header",
        "G4 another key with a jku header",
        "G5 numeric sub",
        "G5 object sub",
        "G5 array of two subs",
    ];

    // AU16: a token that expired 15 s ago is inside the 30 s clock skew and still accepted (A2: real clock; gate item
    // 8: 15 s of slack, and a warm-up request first so a cold host cannot eat the margin).
    [Fact]
    public async Task Post_with_token_expired_inside_the_skew_creates_the_application()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetPathAsync("/health")).StatusCode);
        client.WithToken(factory.Sign(TokenSpec.ExpiredAgo(TimeSpan.FromSeconds(15))));

        var response = await client.PostJsonAsync("/api/applications", Http.BaseBody);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // G4 (gate item 4): regression guard on the resolved bearer options, so a change to any validation setting fails
    // a test even if no token case covers it.
    [Fact]
    public void Bearer_options_pin_the_validation_settings()
    {
        _ = factory.CreateClient();

        var options = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        var parameters = options.TokenValidationParameters;
        Assert.Equal(["ES256"], parameters.ValidAlgorithms);
        Assert.Equal(TimeSpan.FromSeconds(30), parameters.ClockSkew);
        Assert.True(parameters.RequireExpirationTime);
        Assert.True(parameters.RequireSignedTokens);
        Assert.True(parameters.ValidateIssuer);
        Assert.True(parameters.ValidateAudience);
        Assert.True(parameters.ValidateLifetime);
        Assert.True(parameters.ValidateIssuerSigningKey);
        Assert.False(options.IncludeErrorDetails);
        Assert.False(options.MapInboundClaims);
        Assert.Null(options.Authority);
        Assert.Null(options.MetadataAddress);
    }

    // G3 (gate item 3): only the Jwt:* settings decide trust. An Authority (or metadata address) set through the
    // framework's Authentication:Schemes:Bearer section is ignored: no key is fetched from it, the configured key
    // still works, and a token from another key is still a 401.
    [Fact]
    public async Task Bearer_scheme_authority_setting_is_ignored()
    {
        using var host = factory.WithWebHostBuilder(builder => builder
            .UseSetting("Authentication:Schemes:Bearer:Authority", "https://identity.example.invalid")
            .UseSetting("Authentication:Schemes:Bearer:MetadataAddress", "https://identity.example.invalid/.well-known/openid-configuration"));
        using var attackerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var client = host.CreateClient();
        var options = host.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Null(options.Authority);
        Assert.Null(options.MetadataAddress);
        Assert.Equal(HttpStatusCode.Created, (await client.PostJsonAsync("/api/applications", Http.BaseBody)).StatusCode);
        var forged = await client.WithToken(TestTokens.Sign(TokenSpec.Valid(), attackerKey)).PostJsonAsync("/api/applications", Http.BaseBody);
        await forged.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    // G4: the kid header is not a trust anchor. A token signed by the configured key is accepted whatever its kid says
    // (IdentityModel tries every trusted key when no key ID matches); a kid cannot make another key trusted (see the
    // "another key with an unknown kid" bad-token case).
    [Fact]
    public async Task Token_signed_by_the_trusted_key_with_an_unknown_kid_is_accepted()
    {
        var token = TestTokens.Es256(
            new { alg = "ES256", typ = "JWT", kid = "unknown-key-id" }, TestTokens.Payload(TokenSpec.Valid()), factory.SigningKey);
        var client = factory.CreateClient().WithToken(token);

        var response = await client.PostJsonAsync("/api/applications", Http.BaseBody);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // G4: the framework matches the "Bearer" scheme name case-insensitively (RFC 9110 auth schemes are
    // case-insensitive), so "bearer <token>" is accepted.
    [Fact]
    public async Task Lowercase_bearer_scheme_is_accepted()
    {
        var response = await factory.CreateAnonymousClient()
            .PostJsonWithAuthorizationAsync("/api/applications", Http.BaseBody, $"bearer {factory.TokenFor("alice")}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // G4: a token in the query string is not read (only the Authorization header is), so the request is anonymous.
    [Fact]
    public async Task Token_in_the_query_string_is_not_accepted()
    {
        var response = await factory.CreateAnonymousClient()
            .PostJsonAsync($"/api/applications?access_token={factory.TokenFor("alice")}", Http.BaseBody);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).ToString());
    }

    private string BadToken(string name)
    {
        var valid = TokenSpec.Valid(_sentinelOwner);
        var payload = TestTokens.Payload(valid);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return name switch
        {
            "AU11 malformed" => "not-a-jwt",
            "AU13 tampered signature" => TestTokens.TamperSignature(factory.Sign(valid)),
            "AU14 signed with another key" => TestTokens.Sign(valid, otherKey),
            "AU15 expired 45 s ago (past the 30 s skew)" =>
                factory.Sign(TokenSpec.ExpiredAgo(TimeSpan.FromSeconds(45), _sentinelOwner)),
            "AU18 not valid for 5 minutes" =>
                factory.Sign(valid with { NotBefore = DateTime.UtcNow.AddMinutes(5), Expires = DateTime.UtcNow.AddMinutes(20) }),
            "AU20 wrong issuer" => factory.Sign(valid with { Issuer = "https://evil.example" }),
            "AU21 wrong audience" => factory.Sign(valid with { Audience = "other-api" }),
            "AU23 alg none" => TestTokens.AlgNone(valid),
            "AU24 HS256 with the public key DER as secret" =>
                TestTokens.Hs256(valid, factory.SigningKey.ExportSubjectPublicKeyInfo()),
            "AU24 HS256 with the public key PEM as secret" =>
                TestTokens.Hs256(valid, Encoding.ASCII.GetBytes(factory.PublicKeyPem)),
            "AU26 no sub" => factory.Sign(valid with { Subject = null }),
            "AU27 empty sub" => factory.Sign(valid with { Subject = "" }),
            "AU28 whitespace-only sub" => factory.Sign(valid with { Subject = " " }),
            "AU29 129-character sub" => factory.Sign(valid with { Subject = new string('a', 129) }),
            "G4 no exp claim" => TestTokens.Es256(Es256Header, Without(payload, "exp"), factory.SigningKey),
            "G4 ES256 signature under an ES384 header" =>
                TestTokens.Es256(new { alg = "ES384", typ = "JWT" }, payload, factory.SigningKey),
            "G4 another key with an unknown kid" =>
                TestTokens.Es256(new { alg = "ES256", typ = "JWT", kid = "unknown-key-id" }, payload, otherKey),
            "G4 another key with its own jwk header" =>
                TestTokens.Es256(new { alg = "ES256", typ = "JWT", jwk = TestTokens.Jwk(otherKey) }, payload, otherKey),
            "G4 another key with a jku header" =>
                TestTokens.Es256(new { alg = "ES256", typ = "JWT", jku = "https://keys.example.invalid/jwks.json" }, payload, otherKey),
            "G5 numeric sub" => TestTokens.Es256(Es256Header, With(payload, "sub", 12345), factory.SigningKey),
            "G5 object sub" => TestTokens.Es256(Es256Header, With(payload, "sub", new { id = _sentinelOwner }), factory.SigningKey),
            "G5 array of two subs" =>
                TestTokens.Es256(Es256Header, With(payload, "sub", new[] { _sentinelOwner, "bob" }), factory.SigningKey),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown bad token case."),
        };
    }

    private static object Es256Header => new { alg = "ES256", typ = "JWT" };

    private static Dictionary<string, object> Without(Dictionary<string, object> payload, string claim) =>
        payload.Where(kv => kv.Key != claim).ToDictionary();

    private static Dictionary<string, object> With(Dictionary<string, object> payload, string claim, object value) =>
        new(payload) { [claim] = value };

    private async Task<string> OwnerOfAsync(string applicationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return await db.Applications.Where(a => a.Id == Guid.Parse(applicationId)).Select(a => a.OwnerId).SingleAsync(Http.Ct);
    }

    private async Task<int> CountApplicationsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return await db.Applications.CountAsync(Http.Ct);
    }
}
