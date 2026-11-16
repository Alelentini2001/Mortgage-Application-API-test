using System.Diagnostics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 7 telemetry (extends O5): tokens, the Authorization header and the subject (owner ID) reach no log, span
/// or metric tag, and the framework's authentication logs keep IdentityModel's PII hiding (A12).
/// </summary>
[Collection(TelemetryGroup.Name)]
public sealed class AuthTelemetryTests
{
    private const string _sentinelOwner = "sentinel-owner-7";

    // AU90: create, assess, GET, a 404 and a 401 (tampered token) with a sentinel subject leave no token, token
    // segment, Authorization header value or subject in any captured log, span or metric tag. Control: the subject is the owner
    // in the database.
    [Fact]
    public async Task Tokens_and_subject_reach_no_log_span_or_metric_tag()
    {
        await using var factory = new ApiFactory();
        var traced = new TraceparentHandler(ActivityTraceId.CreateRandom());
        var token = factory.TokenFor(_sentinelOwner);
        var tampered = TestTokens.TamperSignature(factory.TokenFor(_sentinelOwner));
        var client = factory.CreateDefaultClient(traced).WithToken(token);
        using var capture = new TelemetryCapture(factory.Services);

        var (applicationId, assessmentId, _) = await Telemetry.AssessAsync(client, Http.BaseBody);
        await client.GetPathAsync($"/api/applications/{applicationId}");
        await (await client.GetPathAsync($"/api/applications/{Guid.NewGuid()}")).AssertProblemAsync(HttpStatusCode.NotFound);
        var rejected = await client.PostJsonWithAuthorizationAsync("/api/applications", Http.BaseBody, $"Bearer {tampered}");
        await rejected.AssertProblemAsync(HttpStatusCode.Unauthorized);
        await capture.WaitForProcessSpanAsync(assessmentId);
        await capture.WaitForMeasurementsAsync(Telemetry.Counter, 1);

        Assert.Equal(_sentinelOwner, await OwnerOfAsync(factory, applicationId));
        var texts = capture.Spans.Select(s => s.Text)
            .Concat(capture.Measurements.Select(m => m.TagText))
            .Concat(factory.Logs.Entries.Select(e => e.Text))
            .ToList();
        Assert.Contains(capture.Spans, s => s.TraceId == traced.TraceId && s.Kind == ActivityKind.Server);
        var forbidden = new[] { token, tampered }.SelectMany(t => t.Split('.')).Where(s => s.Length >= 8)
            .Concat([token, tampered, _sentinelOwner, "Bearer ey"]);
        foreach (var text in forbidden)
        {
            Assert.DoesNotContain(texts, t => t.Contains(text, StringComparison.Ordinal));
        }
    }

    // AU92 (A12): rejected tokens with a sentinel issuer, audience and subject leave none of the three values, nor the
    // token, in the logs at the configured levels. Control: with the authentication categories at Debug the framework
    // does log these failures (at Information, with IdentityModel's exception text), so the check would see them;
    // appsettings.json therefore pins Microsoft.AspNetCore.Authentication at Warning.
    [Fact]
    public async Task Authentication_failure_logs_hold_no_token_values()
    {
        const string issuer = "sentinel-issuer-41";
        const string audience = "sentinel-audience-42";
        const string subject = "sentinel-subject-43";
        await using var control = new ApiFactory();
        using var verbose = control.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Microsoft.AspNetCore.Authentication"] = "Debug",
            })));
        await RejectSentinelTokensAsync(control, verbose.CreateClient().WithoutToken(), issuer, audience, subject);
        Assert.Contains(control.Logs.Entries, e => e.Category.StartsWith("Microsoft.AspNetCore.Authentication", StringComparison.Ordinal)
            && e.Level <= LogLevel.Information);
        // Gate item 9: the control really leaks the issuer, so the assertion below can fail.
        Assert.Contains(control.Logs.Entries, e => e.Text.Contains(issuer, StringComparison.Ordinal));
        await using var factory = new ApiFactory();

        var tokens = await RejectSentinelTokensAsync(factory, factory.CreateAnonymousClient(), issuer, audience, subject);

        foreach (var sentinel in new[] { issuer, audience, subject }.Concat(tokens))
        {
            Assert.DoesNotContain(factory.Logs.Entries, e => e.Text.Contains(sentinel, StringComparison.Ordinal));
        }
    }

    // G10 (gate item 10): with the framework's rejection logs hidden at Warning, the app writes its own
    // TokenRejected event (Information) for a rejected token, holding only the failure's exception type name: no
    // message, token, issuer or subject.
    [Fact]
    public async Task Rejected_token_writes_a_token_rejected_event_with_the_failure_type_only()
    {
        await using var factory = new ApiFactory();
        const string issuer = "sentinel-issuer-51";
        const string subject = "sentinel-subject-52";
        var token = TestTokens.TamperSignature(factory.Sign(TokenSpec.Valid(subject) with { Issuer = issuer }));

        var response = await factory.CreateAnonymousClient()
            .PostJsonWithAuthorizationAsync("/api/applications", Http.BaseBody, $"Bearer {token}");

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
        var entry = Assert.Single(factory.Logs.Entries, e => e.EventName == "TokenRejected");
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.StartsWith("MortgageApplications.", entry.Category, StringComparison.Ordinal);
        Assert.Null(entry.Exception);
        var (key, failureType) = Assert.Single(entry.State, kv => kv.Key != "{OriginalFormat}");
        Assert.Equal("FailureType", key);
        Assert.Matches("^[A-Za-z]+Exception$", failureType);
        foreach (var sentinel in token.Split('.').Append(token).Append(issuer).Append(subject))
        {
            Assert.DoesNotContain(sentinel, entry.Text, StringComparison.Ordinal);
        }
    }

    private static async Task<string[]> RejectSentinelTokensAsync(
        ApiFactory factory, HttpClient client, string issuer, string audience, string subject)
    {
        var tokens = new[]
        {
            factory.Sign(TokenSpec.Valid(subject) with { Issuer = issuer }),
            factory.Sign(TokenSpec.Valid(subject) with { Audience = audience }),
            factory.Sign(TokenSpec.ExpiredAgo(TimeSpan.FromMinutes(5), subject) with { Issuer = issuer, Audience = audience }),
        };
        foreach (var token in tokens)
        {
            var response = await client.PostJsonWithAuthorizationAsync("/api/applications", Http.BaseBody, $"Bearer {token}");
            await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
        }

        return tokens;
    }

    private static async Task<string> OwnerOfAsync(ApiFactory factory, string applicationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return await db.Applications.Where(a => a.Id == Guid.Parse(applicationId)).Select(a => a.OwnerId).SingleAsync(Http.Ct);
    }
}
