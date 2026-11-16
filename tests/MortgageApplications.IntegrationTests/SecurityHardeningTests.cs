using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 8 security hardening (P8-D3, P8-D4, P8-D5): HTTP security headers, <c>Cache-Control: no-store</c>, HSTS
/// outside Development, <c>AllowedHosts</c> host filtering, NuGet lock files and source mapping, and container image
/// digest pinning + Dependabot.
/// </summary>
public sealed class SecurityHardeningTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string _root = FindRepoRoot();

    // S8-01, S8-18 (P8-D3, A3): every response under /api/* (201, 200, 404, 401) carries X-Content-Type-Options: nosniff,
    // X-Frame-Options: DENY, Referrer-Policy: no-referrer, and Cache-Control: no-store.
    [Fact]
    public async Task Api_responses_carry_security_headers_and_no_store_cache_control()
    {
        var client = factory.CreateClient();
        var created = await client.PostJsonAsync("/api/applications", Http.BaseBody);
        var ok = await client.GetPathAsync(created.Headers.Location!.OriginalString);
        var notFound = await client.GetPathAsync($"/api/applications/{Guid.NewGuid()}");
        var unauthorized = await factory.CreateAnonymousClient().PostJsonAsync("/api/applications", Http.BaseBody);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        foreach (var response in new[] { created, ok, notFound, unauthorized })
        {
            AssertSecurityHeaders(response, expectNoStore: true);
        }
    }

    // S8-02 (P8-D3): both localhost and 127.0.0.1:8080 are accepted by AllowedHosts and return security headers on /health.
    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1:8080")]
    public async Task Health_accepts_allowed_hosts_and_returns_security_headers(string hostHeader)
    {
        var client = factory.CreateAnonymousClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        request.Headers.Host = hostHeader;

        var response = await client.SendAsync(request, Http.Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response, expectNoStore: true);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    // S8-03 (P8-D3): POST /dev/token returns Cache-Control: no-store and the baseline security headers.
    [Fact]
    public async Task Dev_token_response_carries_security_headers_and_no_store()
    {
        using var host = factory.WithWebHostBuilder(builder => builder
            .UseSetting("DevAuth:Enabled", "true")
            .UseSetting("Jwt:SigningPublicKey", ""));

        var response = await host.CreateClient().WithoutToken().PostJsonAsync("/dev/token", """{"subject":"alice"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response, expectNoStore: true);
    }

    // S8-14 (P8-D3): AllowedHosts = "localhost;127.0.0.1" rejects an untrusted Host header with 400 Bad Request.
    [Theory]
    [InlineData("/health")]
    [InlineData("/api/applications")]
    public async Task Untrusted_host_header_returns_400(string path)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.Host = "evil.example";

        var response = await client.SendAsync(request, Http.Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // S8-19 (P8-D3, A2): UseHsts() emits Strict-Transport-Security over HTTPS in Production and Staging, and not in Development.
    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public async Task Hsts_header_is_emitted_over_https_outside_development_only(string environment, bool expectHsts)
    {
        using var host = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .UseSetting("AllowedHosts", "localhost;127.0.0.1;example.com"));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://example.com"),
        }).WithoutToken();

        var response = await client.GetPathAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectHsts, response.Headers.Contains("Strict-Transport-Security"));
    }

    // S8-24 (P8-D4): nuget.config maps * to nuget.org over HTTPS, Directory.Build.props enables lock files, all 5
    // packages.lock.json files exist, and verify.sh / Dockerfile / ci.yml use --locked-mode.
    [Fact]
    public void NuGet_lock_files_and_package_source_mapping_are_configured()
    {
        var nugetConfig = XDocument.Load(Path.Combine(_root, "nuget.config"));
        var sources = nugetConfig.Root!.Element("packageSources")!.Elements().ToList();
        Assert.Equal("clear", sources[0].Name.LocalName);
        var add = Assert.Single(sources.Skip(1));
        Assert.Equal("nuget.org", add.Attribute("key")?.Value);
        Assert.Equal("https://api.nuget.org/v3/index.json", add.Attribute("value")?.Value);
        var mapping = nugetConfig.Root.Element("packageSourceMapping")!.Elements("packageSource").Single();
        Assert.Equal("nuget.org", mapping.Attribute("key")?.Value);
        Assert.Equal("*", mapping.Elements("package").Single().Attribute("pattern")?.Value);

        var buildProps = File.ReadAllText(Path.Combine(_root, "Directory.Build.props"));
        Assert.Contains("<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>", buildProps, StringComparison.Ordinal);

        string[] projects =
        [
            "src/MortgageApplications.Core",
            "src/MortgageApplications.Infrastructure",
            "src/MortgageApplications.Api",
            "tests/MortgageApplications.UnitTests",
            "tests/MortgageApplications.IntegrationTests",
        ];
        foreach (var project in projects)
        {
            var lockPath = Path.Combine(_root, project, "packages.lock.json");
            Assert.True(File.Exists(lockPath), $"Missing lock file: {project}/packages.lock.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(lockPath));
            Assert.True(doc.RootElement.TryGetProperty("version", out _));
        }

        Assert.Contains("--locked-mode", File.ReadAllText(Path.Combine(_root, "scripts/verify.sh")), StringComparison.Ordinal);
        Assert.Contains("--locked-mode", File.ReadAllText(Path.Combine(_root, "Dockerfile")), StringComparison.Ordinal);
        Assert.Contains("--locked-mode", File.ReadAllText(Path.Combine(_root, ".github/workflows/ci.yml")), StringComparison.Ordinal);
    }

    // S8-25 (P8-D5): Dockerfile pins sdk:10.0 and aspnet:10.0 by @sha256 digest, and .github/dependabot.yml configures
    // weekly updates for nuget, docker, and github-actions.
    [Fact]
    public void Dockerfile_digests_and_dependabot_are_configured()
    {
        var dockerfile = File.ReadAllText(Path.Combine(_root, "Dockerfile"));
        Assert.Matches(@"FROM mcr\.microsoft\.com/dotnet/sdk:10\.0@sha256:[0-9a-f]{64} AS build", dockerfile);
        Assert.Matches(@"FROM mcr\.microsoft\.com/dotnet/aspnet:10\.0@sha256:[0-9a-f]{64} AS runtime", dockerfile);

        var dependabot = File.ReadAllText(Path.Combine(_root, ".github/dependabot.yml"));
        Assert.Contains("package-ecosystem: \"nuget\"", dependabot, StringComparison.Ordinal);
        Assert.Contains("package-ecosystem: \"docker\"", dependabot, StringComparison.Ordinal);
        Assert.Contains("package-ecosystem: \"github-actions\"", dependabot, StringComparison.Ordinal);
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response, bool expectNoStore)
    {
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        if (expectNoStore)
        {
            Assert.True(response.Headers.CacheControl?.NoStore, "Expected Cache-Control: no-store");
        }
    }

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
