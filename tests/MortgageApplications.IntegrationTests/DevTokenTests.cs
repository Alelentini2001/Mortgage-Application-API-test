using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 7 dev token endpoint <c>POST /dev/token</c> (P7-D3 A): registered only in Development with
/// <c>DevAuth:Enabled=true</c>; signs with a key pair generated in memory at startup (P7-D1 A), which the same host
/// trusts and no other host does. "devOn" hosts set the flag and clear the factory's public key, so the app uses
/// its own key (A1).
/// </summary>
public sealed class DevTokenTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // AU60: the response is exactly {token, expiresAt}; the token is ES256 with sub, iss, aud, iat, nbf, exp and jti;
    // it lives 15 minutes, and expiresAt is its exp.
    [Fact]
    public async Task Dev_token_returns_an_es256_token_for_the_subject()
    {
        using var host = DevOnHost();

        var response = await host.CreateClient().WithoutToken().PostJsonAsync("/dev/token", """{"subject":"alice"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.BodyAsync());
        Assert.Equal(["expiresAt", "token"], body.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        var text = body.RootElement.GetProperty("token").GetString()!;
        Assert.Equal(3, text.Split('.').Length);
        var token = new JsonWebToken(text);
        Assert.Equal("ES256", token.Alg);
        Assert.Equal("alice", token.Subject);
        Assert.Equal(ApiFactory.Issuer, token.Issuer);
        Assert.Equal([ApiFactory.Audience], token.Audiences);
        Assert.False(string.IsNullOrWhiteSpace(token.Id));
        Assert.Equal(token.IssuedAt, token.ValidFrom);
        Assert.Equal(900, (token.ValidTo - token.IssuedAt).TotalSeconds);
        var expiresAt = body.RootElement.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.Equal(token.ValidTo, expiresAt.UtcDateTime);
        Assert.InRange(expiresAt, DateTimeOffset.UtcNow.AddSeconds(895), DateTimeOffset.UtcNow.AddSeconds(905));
    }

    // AU61: a token from the endpoint is accepted by the same host's real validation path.
    [Fact]
    public async Task Dev_token_is_accepted_by_the_api()
    {
        using var host = DevOnHost();
        var client = host.CreateClient().WithToken(await IssueAsync(host, "alice"));

        var created = await client.PostJsonAsync("/api/applications", Http.BaseBody);
        var fetched = await client.GetPathAsync(created.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    // AU64: outside Development the endpoint does not exist, so an authenticated call is a 404 and an anonymous call
    // gets the same status as any unknown path.
    [Theory]
    [MemberData(nameof(NonDevelopmentEnvironments))]
    public async Task Dev_token_outside_development_is_not_registered(string environment)
    {
        using var host = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));

        var authenticated = await host.CreateClient().PostJsonAsync("/dev/token", """{"subject":"alice"}""");
        var anonymous = await host.CreateClient().WithoutToken().PostJsonAsync("/dev/token", """{"subject":"alice"}""");
        var unknown = await host.CreateClient().WithoutToken().PostJsonAsync("/no-such-path", """{"subject":"alice"}""");

        await authenticated.AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(unknown.StatusCode, anonymous.StatusCode);
        Assert.DoesNotContain("token", await anonymous.BodyAsync(), StringComparison.Ordinal);
    }

    public static TheoryData<string> NonDevelopmentEnvironments => ["Staging", "Production"];

    // AU65: Development without the flag does not register the endpoint either.
    [Fact]
    public async Task Dev_token_in_development_without_the_flag_is_not_registered()
    {
        var response = await factory.CreateClient().PostJsonAsync("/dev/token", """{"subject":"alice"}""");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    // AU65 + A10: Development with no key and DevAuth off starts (/health is 200), issues no token, and every
    // protected endpoint is a 401, even for a token that another host would accept.
    [Fact]
    public async Task Development_without_key_or_dev_auth_starts_and_rejects_every_token()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("Jwt:SigningPublicKey", ""));
        var anonymous = host.CreateClient().WithoutToken();

        var health = await anonymous.GetPathAsync("/health");
        var devToken = await anonymous.PostJsonAsync("/dev/token", """{"subject":"alice"}""");
        var unknown = await anonymous.PostJsonAsync("/no-such-path", """{"subject":"alice"}""");
        var create = await host.CreateClient().PostJsonAsync("/api/applications", Http.BaseBody);

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(unknown.StatusCode, devToken.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, devToken.StatusCode);
        await create.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    // AU66: the subject pattern's edges are accepted.
    [Theory]
    [MemberData(nameof(ValidSubjects))]
    public async Task Dev_token_with_valid_subject_returns_200(string subject)
    {
        using var host = DevOnHost();

        var response = await host.CreateClient().WithoutToken().PostJsonAsync("/dev/token", SubjectBody(subject));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static TheoryData<string> ValidSubjects => ["a", new string('a', 64), "a-1-b", "-", "0"];

    // AU67 (A8: the whole string must match, so a trailing newline is rejected): a subject outside the pattern is a
    // 400 ValidationProblemDetails keyed "subject", with no token.
    [Theory]
    [MemberData(nameof(InvalidSubjects))]
    public async Task Dev_token_with_invalid_subject_returns_400_keyed_subject(string subject)
    {
        using var host = DevOnHost();

        var response = await host.CreateClient().WithoutToken().PostJsonAsync("/dev/token", SubjectBody(subject));

        Assert.Equal(["subject"], await response.AssertValidationProblemAsync());
        Assert.DoesNotContain("\"token\"", await response.BodyAsync(), StringComparison.Ordinal);
    }

    public static TheoryData<string> InvalidSubjects =>
        [new string('a', 65), "", "Alice", "al ice", "alice!", "alice\n", "../x", "ali_ce", "alicé", "alice@x"];

    // AU68: a missing, null, empty or mistyped subject, or no body, is a 400 with no token.
    [Theory]
    [MemberData(nameof(MissingSubjectBodies))]
    public async Task Dev_token_without_a_subject_returns_400(string body)
    {
        using var host = DevOnHost();

        var response = await host.CreateClient().WithoutToken().PostJsonAsync("/dev/token", body);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.DoesNotContain("\"token\"", await response.BodyAsync(), StringComparison.Ordinal);
    }

    public static TheoryData<string> MissingSubjectBodies => ["{}", """{"subject":null}""", "", "null", """{"subject":123}"""];

    // AU68 (wrong content type): no token. Routing sends a content type the endpoint does not accept to the
    // framework's 415 endpoint, which is not AllowAnonymous, so an anonymous caller gets the fallback policy's 401
    // ProblemDetails (an authenticated caller gets 415, JsonStrictnessTests I26).
    [Fact]
    public async Task Dev_token_with_wrong_content_type_issues_no_token()
    {
        using var host = DevOnHost();
        var content = new StringContent("""{"subject":"alice"}""", Encoding.UTF8, "text/plain");

        var response = await host.CreateClient().WithoutToken().PostAsync(new Uri("/dev/token", UriKind.Relative), content, Http.Ct);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain("\"token\"", await response.BodyAsync(), StringComparison.Ordinal);
    }

    // AU69: strict JSON, as elsewhere: an extra property or a duplicate subject is a 400.
    [Theory]
    [MemberData(nameof(NonStrictBodies))]
    public async Task Dev_token_with_extra_or_duplicate_property_returns_400(string body)
    {
        using var host = DevOnHost();

        var response = await host.CreateClient().WithoutToken().PostJsonAsync("/dev/token", body);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    public static TheoryData<string> NonStrictBodies =>
        ["""{"subject":"alice","role":"admin"}""", """{"subject":"alice","subject":"bob"}"""];

    // AU71: each host generates its own key in memory, so a token from one host is a 401 on another; control: the
    // issuing host accepts it.
    [Fact]
    public async Task Dev_token_from_one_host_is_rejected_by_another()
    {
        using var first = DevOnHost();
        using var second = DevOnHost();
        var token = await IssueAsync(first, "alice");

        var other = await second.CreateClient().WithToken(token).PostJsonAsync("/api/applications", Http.BaseBody);
        var same = await first.CreateClient().WithToken(token).PostJsonAsync("/api/applications", Http.BaseBody);

        await other.AssertProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.Created, same.StatusCode);
    }

    private WebApplicationFactory<Program> DevOnHost() => DevOn(factory);

    /// <summary>Development, <c>DevAuth:Enabled=true</c> and no configured key: the app's own key is used.</summary>
    internal static WebApplicationFactory<Program> DevOn(ApiFactory factory) =>
        factory.WithWebHostBuilder(builder => builder
            .UseSetting("DevAuth:Enabled", "true")
            .UseSetting("Jwt:SigningPublicKey", ""));

    private static async Task<string> IssueAsync(WebApplicationFactory<Program> host, string subject)
    {
        var response = await host.CreateClient().WithoutToken().PostJsonAsync("/dev/token", SubjectBody(subject));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.BodyAsync());
        return body.RootElement.GetProperty("token").GetString()!;
    }

    private static string SubjectBody(string subject) => JsonSerializer.Serialize(new { subject });
}
