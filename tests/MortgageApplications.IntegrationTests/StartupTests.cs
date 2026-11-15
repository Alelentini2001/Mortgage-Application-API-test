using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

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
    // the connection string is put back, so the failure can only come from the options validation.
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
            });
        }));

        var ex = Assert.ThrowsAny<Exception>(() => missing.CreateClient());

        Assert.NotNull(FindOptionsFailure(ex));
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
