using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Api.Assessments;
using MortgageApplications.Core.Applications;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// I29–I33, I35, I37: unknown, malformed and foreign IDs all return the same 404 ProblemDetails (security S2:
/// no existence leak). The other owner's client shares this factory's database (A12). Phase 7: the other owner is a
/// token for another subject (AU50, AU51), no longer an <c>ICurrentUser</c> override.
/// </summary>
public sealed class NotFoundAndOwnerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // I29
    [Fact]
    public async Task Get_unknown_application_returns_404_problem()
    {
        var response = await _client.GetPathAsync($"/api/applications/{Guid.NewGuid()}");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    // I30 (A2: the {id:guid} constraint means no route matches)
    [Fact]
    public async Task Get_malformed_application_id_returns_404_problem()
    {
        var response = await _client.GetPathAsync("/api/applications/not-a-guid");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    // I31
    [Fact]
    public async Task Post_assessment_for_unknown_application_returns_404_and_saves_nothing()
    {
        var before = await CountAssessmentsAsync();

        var response = await _client.PostJsonAsync($"/api/applications/{Guid.NewGuid()}/assessments", "");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(before, await CountAssessmentsAsync());
    }

    // I32
    [Fact]
    public async Task Get_unknown_assessment_returns_404_problem()
    {
        var applicationId = await _client.CreateApplicationAsync();

        var response = await _client.GetPathAsync($"/api/applications/{applicationId}/assessments/{Guid.NewGuid()}");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    // I33
    [Fact]
    public async Task Get_assessment_under_another_application_returns_404()
    {
        var first = await _client.CreateApplicationAsync();
        var second = await _client.CreateApplicationAsync();
        var assessment = await _client.PostAssessmentAsync(first);
        var assessmentId = await assessment.IdAsync();

        var response = await _client.GetPathAsync($"/api/applications/{second}/assessments/{assessmentId}");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    // I35 / AU50: another owner (a token for another subject) gets 404 everywhere, with the same body as for an
    // unknown ID; the owner still gets 200.
    [Fact]
    public async Task Other_owner_gets_the_same_404_as_an_unknown_id()
    {
        var applicationId = await _client.CreateApplicationAsync();
        var assessmentId = await (await _client.PostAssessmentAsync(applicationId)).IdAsync();
        using var otherFactory = factory.WithWebHostBuilder(_ => { });
        var other = OtherOwnerClient(otherFactory);
        var unknown = await (await other.GetPathAsync($"/api/applications/{Guid.NewGuid()}")).AssertProblemAsync(HttpStatusCode.NotFound);
        // Review F2: the other owner's host must see the same database, or the 404s below would prove nothing.
        Assert.True(await ApplicationExistsAsync(otherFactory.Services, Guid.Parse(applicationId)));
        var assessmentsBefore = await CountAssessmentsAsync();

        var getApplication = await other.GetPathAsync($"/api/applications/{applicationId}");
        var postAssessment = await other.PostJsonAsync($"/api/applications/{applicationId}/assessments", "");
        var getAssessment = await other.GetPathAsync($"/api/applications/{applicationId}/assessments/{assessmentId}");

        foreach (var response in new[] { getApplication, postAssessment, getAssessment })
        {
            var problem = await response.AssertProblemAsync(HttpStatusCode.NotFound);
            Assert.Equal(Strip(unknown), Strip(problem));
        }

        Assert.Equal(assessmentsBefore, await CountAssessmentsAsync());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetPathAsync($"/api/applications/{applicationId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.GetPathAsync($"/api/applications/{applicationId}/assessments/{assessmentId}")).StatusCode);
    }

    // I-5 / AU51: unknown, malformed and another owner's application IDs give the same 404, save no assessment and
    // queue nothing. The other owner's host runs without the worker, so its queue count shows what was enqueued.
    [Fact]
    public async Task Post_assessment_for_unknown_malformed_or_other_owner_application_queues_nothing()
    {
        var applicationId = await _client.CreateApplicationAsync();
        using var otherFactory = factory.WithWebHostBuilder(_ => { });
        var other = OtherOwnerClient(otherFactory);
        var queue = otherFactory.Services.GetRequiredService<ChannelAssessmentQueue>();
        var assessmentsBefore = await CountAssessmentsAsync();

        var unknown = await other.PostAssessmentAsync(Guid.NewGuid().ToString());
        var malformed = await other.PostAssessmentAsync("not-a-guid");
        var otherOwners = await other.PostAssessmentAsync(applicationId);

        var expected = Strip(await unknown.AssertProblemAsync(HttpStatusCode.NotFound));
        Assert.Equal(expected, Strip(await malformed.AssertProblemAsync(HttpStatusCode.NotFound)));
        Assert.Equal(expected, Strip(await otherOwners.AssertProblemAsync(HttpStatusCode.NotFound)));
        Assert.Equal(assessmentsBefore, await CountAssessmentsAsync());
        Assert.Equal(0, queue.Reader.Count);
    }

    // I37: the owner never appears in an assessment or an error.
    [Fact]
    public async Task Assessment_and_error_bodies_never_contain_the_owner()
    {
        const string owner = ApiFactory.DefaultOwner;
        var applicationId = await _client.CreateApplicationAsync();
        var created = await _client.PostAssessmentAsync(applicationId);
        var fetched = await _client.GetPathAsync(created.Headers.Location!.OriginalString);
        var invalid = await _client.PostJsonAsync("/api/applications", Http.BodyWith("""{"termYears":4}"""));

        foreach (var response in new[] { created, fetched, invalid })
        {
            var text = await response.BodyAsync();
            Assert.DoesNotContain("ownerId", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(owner, text, StringComparison.Ordinal);
        }
    }

    private HttpClient OtherOwnerClient(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host) =>
        host.CreateClient().WithToken(factory.TokenFor("other-owner"));

    // Everything except the per-request fields.
    private static string Strip(JsonElement problem) =>
        string.Join(
            "|",
            problem.EnumerateObject()
                .Where(p => p.Name is not ("traceId" or "instance"))
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => $"{p.Name}={p.Value.GetRawText()}"));

    private static async Task<bool> ApplicationExistsAsync(IServiceProvider services, Guid applicationId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return await db.Applications.AnyAsync(a => a.Id == applicationId, Http.Ct);
    }

    private async Task<int> CountAssessmentsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return await db.Set<AffordabilityAssessment>().CountAsync(Http.Ct);
    }
}
