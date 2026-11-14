using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Api.Identity;
using MortgageApplications.Core.Applications;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// I29–I33, I35, I37: unknown, malformed and foreign IDs all return the same 404 ProblemDetails (security S2:
/// no existence leak). The other owner's client shares this factory's database (A12).
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
        var assessment = await _client.PostJsonAsync($"/api/applications/{first}/assessments", "");
        var assessmentId = await IdAsync(assessment);

        var response = await _client.GetPathAsync($"/api/applications/{second}/assessments/{assessmentId}");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    // I35: another owner gets 404 everywhere, with the same body as for an unknown ID; the owner still gets 200.
    [Fact]
    public async Task Other_owner_gets_the_same_404_as_an_unknown_id()
    {
        var applicationId = await _client.CreateApplicationAsync();
        var assessmentId = await IdAsync(await _client.PostJsonAsync($"/api/applications/{applicationId}/assessments", ""));
        using var otherFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<ICurrentUser>(new TestUser("other-owner"))));
        var other = otherFactory.CreateClient();
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

    // I37: the owner never appears in an assessment or an error.
    [Fact]
    public async Task Assessment_and_error_bodies_never_contain_the_owner()
    {
        var owner = factory.Services.GetRequiredService<ICurrentUser>().OwnerId;
        var applicationId = await _client.CreateApplicationAsync();
        var created = await _client.PostJsonAsync($"/api/applications/{applicationId}/assessments", "");
        var fetched = await _client.GetPathAsync(created.Headers.Location!.OriginalString);
        var invalid = await _client.PostJsonAsync("/api/applications", Http.BodyWith("""{"termYears":4}"""));

        foreach (var response in new[] { created, fetched, invalid })
        {
            var text = await response.BodyAsync();
            Assert.DoesNotContain("ownerId", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(owner, text, StringComparison.Ordinal);
        }
    }

    private static async Task<string> IdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.BodyAsync());
        return document.RootElement.GetProperty("id").GetString()!;
    }

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

    private sealed class TestUser(string ownerId) : ICurrentUser
    {
        public string OwnerId { get; } = ownerId;
    }
}
