using System.Text.Json;
using System.Text.RegularExpressions;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// G1 (gate item 1): the local run works with authentication. <c>dotnet run</c> (launchSettings.json) turns the dev
/// token endpoint on in every Development profile, and requests.http gets a token first and sends it on every API
/// request. File-text checks, like ComposeFileTests.
/// </summary>
public sealed partial class LocalRunFileTests
{
    private static readonly string _api = Path.Combine(FindRepoRoot(), "src", "MortgageApplications.Api");

    // G1: every Development launch profile sets DevAuth__Enabled to "true".
    [Fact]
    public void Development_launch_profiles_enable_dev_auth()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_api, "Properties", "launchSettings.json")));

        var development = document.RootElement.GetProperty("profiles").EnumerateObject()
            .Select(p => p.Value.GetProperty("environmentVariables"))
            .Where(v => v.GetProperty("ASPNETCORE_ENVIRONMENT").GetString() == "Development")
            .ToList();

        Assert.NotEmpty(development);
        Assert.All(development, v => Assert.Equal("true", v.GetProperty("DevAuth__Enabled").GetString()));
    }

    // G1: requests.http asks /dev/token for alice and bob, and every request to /api/ sends a bearer token.
    [Fact]
    public void Requests_file_sends_a_token_on_every_api_request()
    {
        var text = File.ReadAllText(Path.Combine(_api, "requests.http"));
        var requests = text.Split("###").Skip(1).ToList();

        var apiRequests = requests.Where(r => r.Contains("{{baseUrl}}/api/", StringComparison.Ordinal)).ToList();

        Assert.Contains(requests, r => r.Contains("POST {{baseUrl}}/dev/token", StringComparison.Ordinal) && r.Contains("\"alice\"", StringComparison.Ordinal));
        Assert.Contains(requests, r => r.Contains("POST {{baseUrl}}/dev/token", StringComparison.Ordinal) && r.Contains("\"bob\"", StringComparison.Ordinal));
        Assert.NotEmpty(apiRequests);
        Assert.All(apiRequests, r => Assert.Matches(BearerHeader(), r));
    }

    [GeneratedRegex(@"\nAuthorization: Bearer \{\{[a-zA-Z]+\.response\.body\.token\}\}")]
    private static partial Regex BearerHeader();

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MortgageApplications.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (MortgageApplications.slnx) not found.");
    }
}
