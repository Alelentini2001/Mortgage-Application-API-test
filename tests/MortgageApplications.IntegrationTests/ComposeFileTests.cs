namespace MortgageApplications.IntegrationTests;

/// <summary>
/// O28 (P5-D2 A): file-text checks of the opt-in observability override. The dashboard UI is on loopback only, its
/// OTLP ports are not published to the host, login stays on, the image tag is pinned, and the default
/// <c>compose.yaml</c> knows nothing about telemetry. <c>docker compose config</c> is a verify step, not a test.
/// </summary>
public sealed class ComposeFileTests
{
    private static readonly string _root = FindRepoRoot();

    // O28: the override adds a pinned, loopback-only, logged-in dashboard and points the API at it.
    [Fact]
    public void Observability_override_adds_a_pinned_loopback_dashboard_and_the_api_endpoint()
    {
        var text = File.ReadAllText(Path.Combine(_root, "compose.observability.yaml"));

        Assert.Matches(@"image: mcr\.microsoft\.com/aspire/dashboard:\d+\.\d+\.\d+\s", text);
        Assert.Contains("\"127.0.0.1:18888:18888\"", text, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"-\s*""?[0-9.:]*1889[0-9]:", text);
        Assert.DoesNotMatch(@"-\s*""?1888[0-9]:", text);
        Assert.DoesNotContain("ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS", text, StringComparison.Ordinal);
        Assert.Contains("OTEL_EXPORTER_OTLP_ENDPOINT: http://aspire-dashboard:18889", text, StringComparison.Ordinal);
        Assert.Contains("depends_on:", text, StringComparison.Ordinal);
    }

    // O28: the default compose file stays as it was: no dashboard and no exporter endpoint.
    [Fact]
    public void Default_compose_file_has_no_telemetry_settings()
    {
        var text = File.ReadAllText(Path.Combine(_root, "compose.yaml"));

        Assert.DoesNotContain("OTEL_", text, StringComparison.Ordinal);
        Assert.DoesNotContain("aspire", text, StringComparison.OrdinalIgnoreCase);
    }

    // AU110 (A13): compose turns the dev token endpoint on, runs as Development, keeps the port on loopback, and no
    // longer says there is no authentication.
    [Fact]
    public void Default_compose_file_enables_dev_auth_on_loopback_only()
    {
        var text = File.ReadAllText(Path.Combine(_root, "compose.yaml"));

        Assert.Contains("DevAuth__Enabled: \"true\"", text, StringComparison.Ordinal);
        Assert.Contains("ASPNETCORE_ENVIRONMENT: Development", text, StringComparison.Ordinal);
        Assert.Contains("- \"127.0.0.1:8080:8080\"", text, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"-\s*""?(0\.0\.0\.0:)?8080:8080", text);
        Assert.DoesNotContain("no authentication", text, StringComparison.OrdinalIgnoreCase);
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
