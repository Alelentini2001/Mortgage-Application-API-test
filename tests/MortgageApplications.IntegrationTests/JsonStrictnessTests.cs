using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>I22–I24, I26: JSON-level failures are 4xx ProblemDetails (A3), never 500, and never echo the input (P3-D8).</summary>
public sealed class JsonStrictnessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // I22: in Development a malformed body throws BadHttpRequestException; it must stay a 400.
    [Fact]
    public async Task Post_malformed_json_returns_400_not_500()
    {
        var response = await _client.PostJsonAsync("/api/applications", """{"annualIncome": 85123.45, """);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.DoesNotContain("85123", await response.BodyAsync(), StringComparison.Ordinal);
    }

    // I23: an unknown property (here an attempt to set the owner) is rejected and nothing is saved.
    [Fact]
    public async Task Post_unknown_property_returns_400_and_saves_nothing()
    {
        var before = await CountApplicationsAsync();

        var response = await _client.PostJsonAsync("/api/applications", Http.BodyWith("""{"ownerId":"attacker-xyz"}"""));

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.DoesNotContain("attacker-xyz", await response.BodyAsync(), StringComparison.Ordinal);
        Assert.Equal(before, await CountApplicationsAsync());
    }

    // I24: numbers sent as strings are rejected.
    [Theory]
    [MemberData(nameof(StringNumbers))]
    public async Task Post_number_as_string_returns_400_without_echo(string overrides, string sentinel)
    {
        var response = await _client.PostJsonAsync("/api/applications", Http.BodyWith(overrides));

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.DoesNotContain(sentinel, await response.BodyAsync(), StringComparison.Ordinal);
    }

    public static TheoryData<string, string> StringNumbers => new()
    {
        { """{"annualIncome":"85123"}""", "85123" },
        { """{"annualIncome":"SENTINEL-9"}""", "SENTINEL-9" },
    };

    // I26
    [Fact]
    public async Task Post_wrong_content_type_returns_415()
    {
        var content = new StringContent(Http.BaseBody, Encoding.UTF8, "text/plain");

        var response = await _client.PostAsync(new Uri("/api/applications", UriKind.Relative), content, Http.Ct);

        await response.AssertProblemAsync(HttpStatusCode.UnsupportedMediaType);
    }

    private async Task<int> CountApplicationsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return await db.Set<Core.Applications.MortgageApplication>().CountAsync(Http.Ct);
    }
}
