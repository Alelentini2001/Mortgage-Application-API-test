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

    // AU92 (A12): with the authentication log categories at Debug, rejected tokens with a sentinel issuer, audience
    // and subject are logged by the framework (control) without any of the three values: IdentityModel's PII hiding
    // stays at its default.
    [Fact]
    public async Task Authentication_failure_logs_hold_no_token_values()
    {
        await using var factory = new ApiFactory();
        using var verbose = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Microsoft.AspNetCore.Authentication"] = "Debug",
            })));
        const string issuer = "sentinel-issuer-41";
        const string audience = "sentinel-audience-42";
        const string subject = "sentinel-subject-43";
        var tokens = new[]
        {
            factory.Sign(TokenSpec.Valid(subject) with { Issuer = issuer }),
            factory.Sign(TokenSpec.Valid(subject) with { Audience = audience }),
            factory.Sign(TokenSpec.ExpiredAgo(TimeSpan.FromMinutes(5), subject) with { Issuer = issuer, Audience = audience }),
        };
        var client = verbose.CreateClient().WithoutToken();

        foreach (var token in tokens)
        {
            var response = await client.PostJsonWithAuthorizationAsync("/api/applications", Http.BaseBody, $"Bearer {token}");
            await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
        }

        var entries = factory.Logs.Entries;
        Assert.Contains(entries, e => e.Category.StartsWith("Microsoft.AspNetCore.Authentication", StringComparison.Ordinal)
            && e.Level <= LogLevel.Information);
        foreach (var sentinel in new[] { issuer, audience, subject }.Concat(tokens))
        {
            Assert.DoesNotContain(entries, e => e.Text.Contains(sentinel, StringComparison.Ordinal));
        }
    }

    private static async Task<string> OwnerOfAsync(ApiFactory factory, string applicationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return await db.Applications.Where(a => a.Id == Guid.Parse(applicationId)).Select(a => a.OwnerId).SingleAsync(Http.Ct);
    }
}
