using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// I22–I24, I26: JSON-level failures are 4xx ProblemDetails (A3), never 500, and never echo the input (P3-D8).
/// Each case runs in Development (binding failures throw and our exception handler writes the body) and in
/// Production (the framework writes an empty 4xx and the status-code middleware writes the body): security L2,
/// review F5.
/// </summary>
public sealed class JsonStrictnessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // (environment, request body, a value that must not come back)
    public static TheoryData<string, string, string> BadBodies
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var environment in new[] { "Development", "Production" })
            {
                // I22: malformed JSON.
                data.Add(environment, """{"annualIncome": 85123.45, """, "85123");
                // I23: an unknown property, here an attempt to set the owner.
                data.Add(environment, Http.BodyWith("""{"ownerId":"attacker-xyz"}"""), "attacker-xyz");
                // I24: numbers sent as strings.
                data.Add(environment, Http.BodyWith("""{"annualIncome":"85123"}"""), "85123");
                data.Add(environment, Http.BodyWith("""{"annualIncome":"SENTINEL-9"}"""), "SENTINEL-9");
                // Security I2: a duplicate property (the last value would otherwise win).
                data.Add(environment, """{"annualIncome":7654321,""" + Http.BaseBody[1..], "7654321");
            }

            return data;
        }
    }

    public static TheoryData<string> Environments => ["Development", "Production"];

    [Theory]
    [MemberData(nameof(BadBodies))]
    public async Task Post_unreadable_body_returns_400_problem_without_echo(string environment, string body, string sentinel)
    {
        using var host = HostFor(environment);
        var client = host.CreateClient();

        var response = await client.PostJsonAsync("/api/applications", body);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.DoesNotContain(sentinel, await response.BodyAsync(), StringComparison.Ordinal);
    }

    // I23: nothing is saved for a rejected body.
    [Fact]
    public async Task Post_unknown_property_saves_nothing()
    {
        var client = factory.CreateClient();
        var before = await CountApplicationsAsync();

        var response = await client.PostJsonAsync("/api/applications", Http.BodyWith("""{"ownerId":"attacker-xyz"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await CountApplicationsAsync());
    }

    // I26
    [Theory]
    [MemberData(nameof(Environments))]
    public async Task Post_wrong_content_type_returns_415_problem(string environment)
    {
        using var host = HostFor(environment);
        var client = host.CreateClient();
        var content = new StringContent(Http.BaseBody, Encoding.UTF8, "text/plain");

        var response = await client.PostAsync(new Uri("/api/applications", UriKind.Relative), content, Http.Ct);

        await response.AssertProblemAsync(HttpStatusCode.UnsupportedMediaType);
    }

    private WebApplicationFactory<Program> HostFor(string environment) =>
        factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));

    private async Task<int> CountApplicationsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return await db.Applications.CountAsync(Http.Ct);
    }
}
