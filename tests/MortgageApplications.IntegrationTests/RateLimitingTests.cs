using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 8 rate limiting (P8-D1 A, A1): a fixed-window policy on <c>POST /api/applications</c>,
/// <c>POST /api/applications/{id}/assessments</c>, and <c>POST /dev/token</c>, partitioned per authenticated
/// <c>sub</c> (and <c>"dev-token"</c> for <c>/dev/token</c>), returning 429 <c>ProblemDetails</c> with
/// <c>Retry-After</c> and saving or queueing nothing.
/// </summary>
public sealed class RateLimitingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // S8-04: PermitLimit = 2 on POST /api/applications allows 1 and 2, rejects 3 with 429 ProblemDetails + Retry-After,
    // and saves only 2 rows.
    [Fact]
    public async Task Post_applications_above_permit_limit_returns_429_problem_and_saves_nothing()
    {
        using var host = WithRateLimit(permitLimit: 2, windowSeconds: 60);
        var before = await CountApplicationsAsync();
        var client = host.CreateClient().WithToken(factory.TokenFor("rate-limit-apps"));

        var first = await client.PostJsonAsync("/api/applications", Http.BaseBody);
        var second = await client.PostJsonAsync("/api/applications", Http.BaseBody);
        var third = await client.PostJsonAsync("/api/applications", Http.BaseBody);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        await AssertTooManyRequestsAsync(third, maxWindowSeconds: 60);
        Assert.Equal(before + 2, await CountApplicationsAsync());
    }

    // S8-05: PermitLimit = 2 on POST /api/applications/{id}/assessments allows 1 and 2, rejects 3 with 429
    // ProblemDetails + Retry-After, and saves only 2 assessments.
    [Fact]
    public async Task Post_assessments_above_permit_limit_returns_429_problem_and_queues_nothing()
    {
        const string owner = "rate-limit-assessments";
        var seeding = factory.CreateClient().WithToken(factory.TokenFor(owner));
        var applicationId = await seeding.CreateApplicationAsync();
        using var host = WithRateLimit(permitLimit: 2, windowSeconds: 60);
        var client = host.CreateClient().WithToken(factory.TokenFor(owner));

        var first = await client.PostAssessmentAsync(applicationId);
        var second = await client.PostAssessmentAsync(applicationId);
        var third = await client.PostAssessmentAsync(applicationId);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        await AssertTooManyRequestsAsync(third, maxWindowSeconds: 60);
        Assert.Equal(2, await CountAssessmentsForAsync(Guid.Parse(applicationId)));
    }

    // S8-06: PermitLimit = 2 on POST /dev/token allows 1 and 2, rejects 3 with 429 ProblemDetails + Retry-After, and
    // issues no token on the 429 response.
    [Fact]
    public async Task Dev_token_above_permit_limit_returns_429_problem_and_issues_no_token()
    {
        using var host = factory.WithWebHostBuilder(builder => builder
            .UseSetting("DevAuth:Enabled", "true")
            .UseSetting("Jwt:SigningPublicKey", "")
            .UseSetting("RateLimiting:PermitLimit", "2")
            .UseSetting("RateLimiting:WindowSeconds", "60"));
        var client = host.CreateClient().WithoutToken();

        var first = await client.PostJsonAsync("/dev/token", """{"subject":"alice"}""");
        var second = await client.PostJsonAsync("/dev/token", """{"subject":"alice"}""");
        var third = await client.PostJsonAsync("/dev/token", """{"subject":"alice"}""");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await AssertTooManyRequestsAsync(third, maxWindowSeconds: 60);
        Assert.DoesNotContain("token", await third.BodyAsync(), StringComparison.OrdinalIgnoreCase);
    }

    // S8-07 (nice): permits replenish after the fixed window rolls over.
    [Fact]
    public async Task Fixed_window_replenishes_permits_after_window_elapses()
    {
        using var host = WithRateLimit(permitLimit: 1, windowSeconds: 1);
        var client = host.CreateClient().WithToken(factory.TokenFor("rate-limit-replenish"));

        var first = await client.PostJsonAsync("/api/applications", Http.BaseBody);
        var second = await client.PostJsonAsync("/api/applications", Http.BaseBody);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        await AssertTooManyRequestsAsync(second, maxWindowSeconds: 1);
        await Http.EventuallyAsync(
            async () => (await client.PostJsonAsync("/api/applications", Http.BaseBody)).StatusCode == HttpStatusCode.Created,
            () => "fixed window replenishment");
    }

    // S8-15: rate-limit partitions are isolated per sub, so alice hitting 429 does not block bob.
    [Fact]
    public async Task Rate_limit_is_partitioned_per_subject()
    {
        using var host = WithRateLimit(permitLimit: 1, windowSeconds: 60);
        var alice = host.CreateClient().WithToken(factory.TokenFor("rate-limit-alice"));
        var bob = host.CreateClient().WithToken(factory.TokenFor("rate-limit-bob"));

        var aliceFirst = await alice.PostJsonAsync("/api/applications", Http.BaseBody);
        var aliceSecond = await alice.PostJsonAsync("/api/applications", Http.BaseBody);
        var bobFirst = await bob.PostJsonAsync("/api/applications", Http.BaseBody);

        Assert.Equal(HttpStatusCode.Created, aliceFirst.StatusCode);
        await AssertTooManyRequestsAsync(aliceSecond, maxWindowSeconds: 60);
        Assert.Equal(HttpStatusCode.Created, bobFirst.StatusCode);
    }

    // S8-16 (A1): exhausting a caller's shared write budget (POST application + POST assessment) leaves GET endpoints
    // and /health unthrottled so polling continues to work.
    [Fact]
    public async Task Exhausted_write_budget_does_not_throttle_get_or_health_endpoints()
    {
        using var host = WithRateLimit(permitLimit: 2, windowSeconds: 60);
        var client = host.CreateClient().WithToken(factory.TokenFor("rate-limit-reads"));

        var applicationId = await client.CreateApplicationAsync();
        var assessmentResponse = await client.PostAssessmentAsync(applicationId);
        var assessmentLocation = assessmentResponse.Headers.Location!.OriginalString;
        var throttled = await client.PostAssessmentAsync(applicationId);

        await AssertTooManyRequestsAsync(throttled, maxWindowSeconds: 60);
        var getApp = await client.GetPathAsync($"/api/applications/{applicationId}");
        var getAssessment = await client.GetPathAsync(assessmentLocation);
        var getHealth = await client.WithoutToken().GetPathAsync("/health");
        Assert.Equal(HttpStatusCode.OK, getApp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getAssessment.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getHealth.StatusCode);
    }

    // S8-17: authentication runs before rate limiting on /api/*, so anonymous or bad-token requests return 401 and
    // never consume a user's permits.
    [Fact]
    public async Task Unauthenticated_requests_return_401_and_do_not_consume_rate_limit_permits()
    {
        using var host = WithRateLimit(permitLimit: 1, windowSeconds: 60);
        var anonymous = host.CreateClient().WithoutToken();
        var alice = host.CreateClient().WithToken(factory.TokenFor("rate-limit-auth-order"));

        for (var i = 0; i < 3; i++)
        {
            var unauth = await anonymous.PostJsonAsync("/api/applications", Http.BaseBody);
            await unauth.AssertProblemAsync(HttpStatusCode.Unauthorized);
        }

        var authorized = await alice.PostJsonAsync("/api/applications", Http.BaseBody);
        Assert.Equal(HttpStatusCode.Created, authorized.StatusCode);
    }

    private WebApplicationFactory<Program> WithRateLimit(int permitLimit, int windowSeconds) =>
        factory.WithWebHostBuilder(builder => builder
            .UseSetting("RateLimiting:PermitLimit", permitLimit.ToString(CultureInfo.InvariantCulture))
            .UseSetting("RateLimiting:WindowSeconds", windowSeconds.ToString(CultureInfo.InvariantCulture)));

    private static async Task AssertTooManyRequestsAsync(HttpResponseMessage response, int maxWindowSeconds)
    {
        await response.AssertProblemAsync(HttpStatusCode.TooManyRequests);
        Assert.NotNull(response.Headers.RetryAfter?.Delta);
        var seconds = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
        Assert.InRange(seconds, 1, maxWindowSeconds);
    }

    private async Task<int> CountApplicationsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return await db.Applications.CountAsync(Http.Ct);
    }

    private async Task<int> CountAssessmentsForAsync(Guid applicationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return await db.Assessments.CountAsync(a => a.ApplicationId == applicationId, Http.Ct);
    }
}
