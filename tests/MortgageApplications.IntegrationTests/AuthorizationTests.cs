using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Api.Assessments;
using MortgageApplications.Core.Applications;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 7 authorization: the fallback policy covers every endpoint except a literal allowlist (AU40, AU42), and the
/// owner comes only from the token's <c>sub</c> (AU52, AU54). AU50 and AU51 are the converted I35 and I-5 in
/// <see cref="NotFoundAndOwnerTests"/>.
/// </summary>
public sealed partial class AuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] _applicationEndpoints =
    [
        "POST /api/applications",
        "GET /api/applications/{id:guid}",
        "POST /api/applications/{id:guid}/assessments",
        "GET /api/applications/{id:guid}/assessments/{assessmentId:guid}",
    ];

    // AU40: only the allowlisted endpoints are anonymous (A3: OpenAPI and Scalar are anonymous in Development, where
    // they are mapped), with and without the dev token endpoint. Every other endpoint, found from the endpoint data
    // source so a new one cannot slip through, returns 401 to an anonymous call.
    [Theory]
    [MemberData(nameof(AllowlistCases))]
    public async Task Every_endpoint_outside_the_allowlist_requires_a_token(bool devAuth, string[] allowlist)
    {
        using var host = factory.WithWebHostBuilder(builder =>
        {
            if (devAuth)
            {
                builder.UseSetting("DevAuth:Enabled", "true");
            }
        });
        var client = host.CreateClient().WithoutToken();
        var endpoints = host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .SelectMany(e => Methods(e).Select(method => (Name: $"{method} {e.RoutePattern.RawText}", Endpoint: e)))
            .ToList();

        var anonymous = endpoints.Where(e => e.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.Name).Order(StringComparer.Ordinal).ToArray();
        var protectedOnes = endpoints.Where(e => e.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null).ToList();

        Assert.Equal(allowlist.Order(StringComparer.Ordinal).ToArray(), anonymous);
        Assert.Subset(protectedOnes.Select(e => e.Name).ToHashSet(), _applicationEndpoints.ToHashSet());
        foreach (var (name, endpoint) in protectedOnes)
        {
            var method = name[..name.IndexOf(' ', StringComparison.Ordinal)];
            var path = RouteParameter().Replace(endpoint.RoutePattern.RawText!, _ => Guid.NewGuid().ToString());
            using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative))
            {
                Content = new StringContent(Http.BaseBody, Encoding.UTF8, "application/json"),
            };
            var response = await client.SendAsync(request, Http.Ct);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{name} returned {(int)response.StatusCode} to an anonymous call.");
        }
    }

    public static TheoryData<bool, string[]> AllowlistCases => new()
    {
        { false, DevelopmentAllowlist },
        { true, [.. DevelopmentAllowlist, "POST /dev/token"] },
    };

    // A3: /health, plus the OpenAPI document and Scalar, which are mapped in Development only.
    private static string[] DevelopmentAllowlist =>
        ["GET /health", "GET /openapi/{documentName}.json", "GET /scalar/{documentName?}"];

    // AU42: each application endpoint, called without a token, is a 401 even for an existing application, and
    // creates and queues nothing (the host runs without the worker, so the queue count shows what was enqueued).
    [Fact]
    public async Task Application_endpoints_without_token_return_401_and_queue_nothing()
    {
        using var host = factory.WithWebHostBuilder(_ => { });
        var owner = host.CreateClient();
        var applicationId = await owner.CreateApplicationAsync();
        var assessmentId = await (await owner.PostAssessmentAsync(applicationId)).IdAsync();
        var queue = host.Services.GetRequiredService<ChannelAssessmentQueue>();
        var queuedBefore = queue.Reader.Count;
        var (applicationsBefore, assessmentsBefore) = await CountsAsync();
        var anonymous = host.CreateClient().WithoutToken();

        var responses = new[]
        {
            await anonymous.PostJsonAsync("/api/applications", Http.BaseBody),
            await anonymous.GetPathAsync($"/api/applications/{applicationId}"),
            await anonymous.PostAssessmentAsync(applicationId),
            await anonymous.GetPathAsync($"/api/applications/{applicationId}/assessments/{assessmentId}"),
        };

        foreach (var response in responses)
        {
            await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
        }

        Assert.Equal((applicationsBefore, assessmentsBefore), await CountsAsync());
        Assert.Equal(queuedBefore, queue.Reader.Count);
    }

    // AU52: the other direction of AU50: bob's application is a 404 for the default owner.
    [Fact]
    public async Task Default_owner_gets_404_for_another_subjects_application()
    {
        var bob = factory.CreateClient().WithToken(factory.TokenFor("bob"));
        var applicationId = await bob.CreateApplicationAsync();

        var response = await factory.CreateClient().GetPathAsync($"/api/applications/{applicationId}");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.OK, (await bob.GetPathAsync($"/api/applications/{applicationId}")).StatusCode);
    }

    // AU54: headers and a query string that claim another owner have no effect; the owner is the token's sub.
    [Fact]
    public async Task Owner_headers_and_query_are_ignored_and_the_sub_owns_the_application()
    {
        var client = factory.CreateClient();
        foreach (var header in new[] { "X-Owner-Id", "X-User-Id", "X-Forwarded-User" })
        {
            client.DefaultRequestHeaders.Add(header, "bob");
        }

        var created = await client.PostJsonAsync("/api/applications?ownerId=bob", Http.BaseBody);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var applicationId = await created.IdAsync();
        Assert.Equal(ApiFactory.DefaultOwner, await OwnerOfAsync(applicationId));
        var bob = factory.CreateClient().WithToken(factory.TokenFor("bob"));
        await (await bob.GetPathAsync($"/api/applications/{applicationId}?ownerId=bob")).AssertProblemAsync(HttpStatusCode.NotFound);
    }

    private static IEnumerable<string> Methods(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"];

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameter();

    private async Task<string> OwnerOfAsync(string applicationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return await db.Applications.Where(a => a.Id == Guid.Parse(applicationId)).Select(a => a.OwnerId).SingleAsync(Http.Ct);
    }

    private async Task<(int Applications, int Assessments)> CountsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return (await db.Applications.CountAsync(Http.Ct), await db.Set<AffordabilityAssessment>().CountAsync(Http.Ct));
    }
}
