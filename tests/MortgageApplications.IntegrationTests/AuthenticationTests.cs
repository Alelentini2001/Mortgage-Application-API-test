using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    // AU11, AU13–AU15, AU18, AU20, AU21, AU23, AU24, AU26–AU29 (AU31 and AU32 checked on each): every bad token is a
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
        "AU15 expired 35 s ago (past the 30 s skew)",
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
    ];

    // AU16: a token that expired 25 s ago is inside the 30 s clock skew and still accepted (A2: real clock, 5 s slack).
    [Fact]
    public async Task Post_with_token_expired_inside_the_skew_creates_the_application()
    {
        var client = factory.CreateClient().WithToken(factory.Sign(TokenSpec.ExpiredAgo(TimeSpan.FromSeconds(25))));

        var response = await client.PostJsonAsync("/api/applications", Http.BaseBody);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private string BadToken(string name)
    {
        var valid = TokenSpec.Valid(_sentinelOwner);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return name[..4] switch
        {
            "AU11" => "not-a-jwt",
            "AU13" => TestTokens.TamperSignature(factory.Sign(valid)),
            "AU14" => TestTokens.Sign(valid, otherKey),
            "AU15" => factory.Sign(TokenSpec.ExpiredAgo(TimeSpan.FromSeconds(35), _sentinelOwner)),
            "AU18" => factory.Sign(valid with { NotBefore = DateTime.UtcNow.AddMinutes(5), Expires = DateTime.UtcNow.AddMinutes(20) }),
            "AU20" => factory.Sign(valid with { Issuer = "https://evil.example" }),
            "AU21" => factory.Sign(valid with { Audience = "other-api" }),
            "AU23" => TestTokens.AlgNone(valid),
            "AU24" when name.EndsWith("DER as secret", StringComparison.Ordinal) =>
                TestTokens.Hs256(valid, factory.SigningKey.ExportSubjectPublicKeyInfo()),
            "AU24" => TestTokens.Hs256(valid, Encoding.ASCII.GetBytes(factory.PublicKeyPem)),
            "AU26" => factory.Sign(valid with { Subject = null }),
            "AU27" => factory.Sign(valid with { Subject = "" }),
            "AU28" => factory.Sign(valid with { Subject = " " }),
            "AU29" => factory.Sign(valid with { Subject = new string('a', 129) }),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown bad token case."),
        };
    }

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
