using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>I42–I46: startup options validation (security S4), /health, and OpenAPI/Scalar in Development only.</summary>
public sealed class StartupTests
{
    // I42: an invalid option stops the app at startup; the message names the option, not the value.
    [Fact]
    public void Startup_with_invalid_assessment_rate_fails()
    {
        using var factory = new ApiFactory();
        using var invalid = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Affordability:AssessmentRate"] = "0.987" })));

        var ex = Assert.ThrowsAny<Exception>(() => invalid.CreateClient());

        var failure = FindOptionsFailure(ex);
        Assert.Contains("AssessmentRate", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("0.987", failure.Message, StringComparison.Ordinal);
    }

    // I43: no Affordability section must not start with zero limits. Review F4: only that section is missing;
    // the connection string (and, from Phase 7, the Jwt section) is put back, so the failure can only come from the
    // affordability options validation.
    [Fact]
    public void Startup_without_affordability_section_fails()
    {
        using var factory = new ApiFactory();
        using var missing = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MortgageApplications"] = "Data Source=:memory:",
                ["Jwt:Issuer"] = ApiFactory.Issuer,
                ["Jwt:Audience"] = ApiFactory.Audience,
                ["Jwt:SigningPublicKey"] = factory.PublicKeyPem,
            });
        }));

        var ex = Assert.ThrowsAny<Exception>(() => missing.CreateClient());

        Assert.NotNull(FindOptionsFailure(ex));
    }

    // AU80 (+ AU81 for Staging): DevAuth:Enabled=true outside Development stops the app at startup (fail closed),
    // even with a valid key; the message names the setting.
    [Theory]
    [MemberData(nameof(NonDevelopmentEnvironments))]
    public void Startup_with_dev_auth_outside_development_fails(string environment)
    {
        using var factory = new ApiFactory();
        using var host = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .UseSetting("DevAuth:Enabled", "true"));

        var ex = Assert.ThrowsAny<Exception>(() => host.CreateClient());

        Assert.Contains("DevAuth:Enabled", FindOptionsFailure(ex).Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> NonDevelopmentEnvironments => ["Production", "Staging"];

    // AU82: outside Development, no signing public key stops the app at startup; the message names the setting.
    [Fact]
    public void Startup_in_production_without_a_signing_key_fails()
    {
        using var factory = new ApiFactory();
        using var host = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("Jwt:SigningPublicKey", ""));

        var ex = Assert.ThrowsAny<Exception>(() => host.CreateClient());

        Assert.Contains("Jwt:SigningPublicKey", FindOptionsFailure(ex).Message, StringComparison.Ordinal);
    }

    // AU83 (A14): Production with a configured key starts; /health is anonymous, the API needs a token.
    [Fact]
    public async Task Startup_in_production_with_a_signing_key_requires_a_token()
    {
        using var factory = new ApiFactory();
        using var host = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var anonymous = host.CreateClient().WithoutToken();

        var health = await anonymous.GetPathAsync("/health");
        var withoutToken = await anonymous.PostJsonAsync("/api/applications", Http.BaseBody);
        var withToken = await host.CreateClient().PostJsonAsync("/api/applications", Http.BaseBody);

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        await withoutToken.AssertProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.Created, withToken.StatusCode);
    }

    // AU85 (A9, A11): an invalid Jwt option stops the app at startup; the message names the setting, not the value.
    [Theory]
    [MemberData(nameof(InvalidJwtSettings))]
    public void Startup_with_invalid_jwt_setting_fails(string key, string value)
    {
        using var factory = new ApiFactory();
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        var ex = Assert.ThrowsAny<Exception>(() => host.CreateClient());

        var message = FindOptionsFailure(ex).Message;
        Assert.Contains(key, message, StringComparison.Ordinal);
        if (value.Trim().Length > 1)
        {
            Assert.DoesNotContain(value, message, StringComparison.Ordinal);
        }
    }

    public static TheoryData<string, string> InvalidJwtSettings => new()
    {
        { "Jwt:Issuer", "" },
        { "Jwt:Audience", " " },
        { "Jwt:TokenLifetimeMinutes", "0" },
        { "Jwt:TokenLifetimeMinutes", "-1" },
        { "Jwt:TokenLifetimeMinutes", "61" },
        { "Jwt:SigningPublicKey", "not-a-pem-key" },
    };

    // Review gate: the exact valid limits of Jwt:TokenLifetimeMinutes (1 and 60) start the app.
    [Theory]
    [MemberData(nameof(ValidTokenLifetimeLimits))]
    public async Task Startup_with_token_lifetime_at_a_limit_starts(string minutes)
    {
        using var factory = new ApiFactory();
        using var valid = factory.WithWebHostBuilder(builder => builder.UseSetting("Jwt:TokenLifetimeMinutes", minutes));

        var response = await valid.CreateClient().GetPathAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static TheoryData<string> ValidTokenLifetimeLimits => ["1", "60"];

    // G2 (gate item 2, fail closed): the dev token endpoint and a configured signing key cannot be on together, so
    // the dev key is always the only trusted key when DevAuth is on. The message names both settings, not the key.
    [Fact]
    public void Startup_with_dev_auth_and_a_signing_key_fails()
    {
        using var factory = new ApiFactory();
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("DevAuth:Enabled", "true"));

        var ex = Assert.ThrowsAny<Exception>(() => host.CreateClient());

        var message = FindOptionsFailure(ex).Message;
        Assert.Contains("DevAuth:Enabled", message, StringComparison.Ordinal);
        Assert.Contains("Jwt:SigningPublicKey", message, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.PublicKeyPem.Split('\n')[1], message, StringComparison.Ordinal);
    }

    // G6 (gate item 6): a signing key of the wrong kind stops the app at startup, and the key text is not in the
    // message: a P-384 or an RSA public key, or any private key.
    [Theory]
    [MemberData(nameof(WrongKindsOfKey))]
    public void Startup_with_wrong_kind_of_signing_key_fails(string kind)
    {
        var pem = kind switch
        {
            "P-384 public key" => ECDsa.Create(ECCurve.NamedCurves.nistP384).ExportSubjectPublicKeyInfoPem(),
            "RSA public key" => RSA.Create(2048).ExportSubjectPublicKeyInfoPem(),
            "P-256 private key (PKCS#8)" => ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportPkcs8PrivateKeyPem(),
            "P-256 private key (SEC 1)" => ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportECPrivateKeyPem(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown key kind."),
        };
        using var factory = new ApiFactory();
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("Jwt:SigningPublicKey", pem));

        var ex = Assert.ThrowsAny<Exception>(() => host.CreateClient());

        var message = FindOptionsFailure(ex).Message;
        Assert.Contains("Jwt:SigningPublicKey", message, StringComparison.Ordinal);
        Assert.DoesNotContain(pem.Split('\n')[1], message, StringComparison.Ordinal);
    }

    public static TheoryData<string> WrongKindsOfKey =>
        ["P-384 public key", "RSA public key", "P-256 private key (PKCS#8)", "P-256 private key (SEC 1)"];

    // AU87: no Jwt section at all does not start with defaults. Only that section is missing: the connection string
    // and the Affordability section are put back.
    [Fact]
    public void Startup_without_jwt_section_fails()
    {
        using var factory = new ApiFactory();
        using var missing = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MortgageApplications"] = "Data Source=:memory:",
                ["Affordability:RulesVersion"] = "1.0",
                ["Affordability:AssessmentRate"] = "0.06",
                ["Affordability:MaxLoanToValuePercent"] = "90",
                ["Affordability:MaxDebtToIncomePercent"] = "45",
                ["Affordability:MaxLoanToIncomeMultiple"] = "4.5",
            });
        }));

        var ex = Assert.ThrowsAny<Exception>(() => missing.CreateClient());

        Assert.Contains("Jwt:", FindOptionsFailure(ex).Message, StringComparison.Ordinal);
    }

    // Q7: the queue capacity is validated at startup (1 to 10,000), like the affordability options.
    [Theory]
    [MemberData(nameof(InvalidQueueCapacities))]
    public void Startup_with_invalid_queue_capacity_fails(string capacity)
    {
        using var factory = new ApiFactory();
        using var invalid = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Assessments:QueueCapacity"] = capacity })));

        var ex = Assert.ThrowsAny<Exception>(() => invalid.CreateClient());

        Assert.Contains("QueueCapacity", FindOptionsFailure(ex).Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> InvalidQueueCapacities => ["0", "10001"];

    // R3: the startup re-enqueue retry delay is validated at startup (1 to 3,600 seconds).
    [Theory]
    [MemberData(nameof(InvalidRetryDelays))]
    public void Startup_with_invalid_requeue_retry_delay_fails(string seconds)
    {
        using var factory = new ApiFactory();
        using var invalid = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Assessments:RequeueRetryDelaySeconds"] = seconds })));

        var ex = Assert.ThrowsAny<Exception>(() => invalid.CreateClient());

        Assert.Contains("RequeueRetryDelaySeconds", FindOptionsFailure(ex).Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> InvalidRetryDelays => ["0", "3601"];

    // Re-review: the exact limits of the retry delay start the app.
    [Theory]
    [MemberData(nameof(ValidRetryDelayLimits))]
    public async Task Startup_with_requeue_retry_delay_at_a_limit_starts(string seconds)
    {
        using var factory = new ApiFactory();
        using var valid = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Assessments:RequeueRetryDelaySeconds"] = seconds })));

        var response = await valid.CreateClient().GetPathAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static TheoryData<string> ValidRetryDelayLimits => ["1", "3600"];

    // I44 (replaces the Phase 0 placeholder)
    [Fact]
    public async Task Health_returns_200()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetPathAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // I45: the OpenAPI document explains that MaxBorrowing is income-based (rules section 2).
    [Fact]
    public async Task OpenApi_in_development_documents_max_borrowing()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetPathAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("income-based maximum borrowing; ignores property value and deposit", await response.BodyAsync(), StringComparison.Ordinal);
    }

    // I46
    [Theory]
    [MemberData(nameof(DevelopmentOnlyPaths))]
    public async Task Development_only_path_returns_404_in_production(string path)
    {
        using var factory = new ApiFactory();
        using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var client = production.CreateClient();

        var response = await client.GetPathAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static TheoryData<string> DevelopmentOnlyPaths => ["/openapi/v1.json", "/scalar", "/scalar/v1"];

    // AU100: in Development the OpenAPI document is anonymous (A3) and declares an http bearer JWT scheme; the
    // protected operations require it, /health and /dev/token do not.
    [Fact]
    public async Task OpenApi_in_development_declares_the_bearer_scheme_on_protected_operations()
    {
        using var factory = new ApiFactory();
        using var host = DevTokenTests.DevOn(factory);

        var response = await host.CreateClient().WithoutToken().GetPathAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = System.Text.Json.JsonDocument.Parse(await response.BodyAsync());
        var root = document.RootElement;
        var (name, scheme) = Assert.Single(root.GetProperty("components").GetProperty("securitySchemes").EnumerateObject()
            .Select(p => (p.Name, p.Value)));
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", scheme.GetProperty("bearerFormat").GetString());
        var paths = root.GetProperty("paths");
        foreach (var (path, method) in new[]
        {
            ("/api/applications", "post"),
            ("/api/applications/{id}", "get"),
            ("/api/applications/{id}/assessments", "post"),
            ("/api/applications/{id}/assessments/{assessmentId}", "get"),
        })
        {
            var security = paths.GetProperty(path).GetProperty(method).GetProperty("security");
            Assert.Contains(security.EnumerateArray(), requirement => requirement.TryGetProperty(name, out _));
        }

        Assert.False(paths.GetProperty("/health").GetProperty("get").TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/dev/token").GetProperty("post").TryGetProperty("security", out _));
    }

    // AU101 (I46 without a token): in Production an anonymous request for a Development-only path gets the same
    // status as any unknown path, never the document.
    [Theory]
    [MemberData(nameof(DevelopmentOnlyPaths))]
    public async Task Development_only_path_without_token_in_production_is_like_an_unknown_path(string path)
    {
        using var factory = new ApiFactory();
        using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var anonymous = production.CreateClient().WithoutToken();

        var response = await anonymous.GetPathAsync(path);
        var unknown = await anonymous.GetPathAsync("/no-such-path");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(unknown.StatusCode, response.StatusCode);
    }

    // O27 (X1, carried over from Phase 4): outside Development the app does not migrate its database at startup;
    // Development does (control). The host uses a fresh named in-memory database that ApiFactory did not migrate.
    [Theory]
    [MemberData(nameof(MigrationCases))]
    public async Task Startup_migrates_the_database_only_in_development(string environment, long expectedHistoryTables)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"x1-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
        using var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();
        using var factory = new ApiFactory();
        using var host = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<MortgageApplicationsDbContext>>();
                services.AddDbContext<MortgageApplicationsDbContext>(options => options.UseSqlite(connectionString));
            }));

        var response = await host.CreateClient().GetPathAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var command = keepAlive.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory'";
        Assert.Equal(expectedHistoryTables, (long)(await command.ExecuteScalarAsync(Http.Ct))!);
        SqliteConnection.ClearPool(keepAlive);
    }

    public static TheoryData<string, long> MigrationCases => new() { { "Production", 0 }, { "Development", 1 } };

    private static OptionsValidationException FindOptionsFailure(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is OptionsValidationException options)
            {
                return options;
            }

            if (current is AggregateException aggregate && aggregate.InnerExceptions.Count > 0)
            {
                current = aggregate.InnerExceptions[0];
                if (current is OptionsValidationException inner)
                {
                    return inner;
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"No OptionsValidationException in: {ex.GetType().Name}: {ex.Message}");
    }
}
