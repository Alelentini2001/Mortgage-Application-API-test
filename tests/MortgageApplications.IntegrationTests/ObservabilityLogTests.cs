using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 5 logs: the new queued log, the framework's 500 log (P5-D4) without values and with the request's trace ID,
/// the trace ID in every ProblemDetails for a sent <c>traceparent</c> (extends I40), and EF Core command logs at
/// Warning (P5-D5).
/// </summary>
[Collection(TelemetryGroup.Name)]
public sealed class ObservabilityLogTests
{
    private const string _efCommand = "Microsoft.EntityFrameworkCore.Database.Command";

    // O20 (Q7): a successful queue logs AssessmentQueued (Information) with the assessment and application IDs only,
    // inside the request's trace.
    [Fact]
    public async Task Post_assessment_logs_assessment_queued_with_the_two_ids_only()
    {
        await using var factory = new ApiFactory();
        var traced = new TraceparentHandler(ActivityTraceId.CreateRandom());
        var client = factory.CreateDefaultClient(traced);
        var applicationId = await client.CreateApplicationAsync();

        var posted = await client.PostAssessmentAsync(applicationId);

        var entry = Assert.Single(factory.Logs.Entries, e => e.EventName == "AssessmentQueued");
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("MortgageApplications.Api.Applications.ApplicationEndpoints", entry.Category);
        Assert.Equal(
            [
                new KeyValuePair<string, string?>("AssessmentId", await posted.IdAsync()),
                new KeyValuePair<string, string?>("ApplicationId", applicationId),
            ],
            entry.State.Where(kv => kv.Key != "{OriginalFormat}"));
        Assert.Equal(traced.TraceId, entry.TraceId);
    }

    // O21: nothing is logged as queued for an unknown application or another owner's; control: the owner's own POST is.
    [Fact]
    public async Task Post_assessment_for_unknown_or_foreign_application_logs_no_queued_entry()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var applicationId = await client.CreateApplicationAsync();
        var other = factory.CreateClient().WithToken(factory.TokenFor("other-owner"));

        await (await other.PostAssessmentAsync(applicationId)).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await client.PostAssessmentAsync(Guid.NewGuid().ToString())).AssertProblemAsync(HttpStatusCode.NotFound);

        Assert.DoesNotContain(factory.Logs.Entries, e => e.EventName == "AssessmentQueued");
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAssessmentAsync(applicationId)).StatusCode);
        Assert.Single(factory.Logs.Entries, e => e.EventName == "AssessmentQueued");
    }

    // O6 (P5-D4, security S1 b): a real 500 is logged by the framework with its exception, and the ProblemDetails
    // traceId and the log's trace ID are the trace ID of the sent traceparent. Security gate: neither the logs nor the
    // request's server span (tags, status description, events) carry a sentinel input or the owner ID.
    [Fact]
    public async Task Real_500_is_logged_without_values_and_carries_the_request_trace_id()
    {
        await using var factory = new ApiFactory();
        using var throwing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddScoped<IMortgageApplicationRepository, ValueFreeThrowingRepository>()));
        var traced = new TraceparentHandler(ActivityTraceId.CreateRandom());
        var client = throwing.CreateDefaultClient(traced);
        using var capture = new TelemetryCapture(throwing.Services);

        var response = await client.PostJsonAsync("/api/applications", Telemetry.SentinelBody);

        var problem = await response.AssertProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Contains(traced.TraceId, problem.GetProperty("traceId").GetString(), StringComparison.Ordinal);
        var error = Assert.Single(factory.Logs.Entries, e => e.Level >= LogLevel.Error);
        Assert.Contains(nameof(InvalidOperationException), error.Exception, StringComparison.Ordinal);
        Assert.Equal(traced.TraceId, error.TraceId);
        var span = await capture.WaitForSpanAsync(
            s => s.TraceId == traced.TraceId && s.Kind == ActivityKind.Server, "the 500 request's server span");
        const string owner = ApiFactory.DefaultOwner;
        var clean = new ForbiddenNumbers([], []);
        var texts = factory.Logs.Entries.Select(e => e.Text).Append(span.Text).Select(clean.Sanitize).ToList();
        foreach (var sentinel in new[] { "85123", "912.34", "61234", "301234", owner })
        {
            Assert.DoesNotContain(texts, t => t.Contains(sentinel, StringComparison.Ordinal));
        }
    }

    // O7 (extends I40): a 404 ProblemDetails carries the trace ID of the sent traceparent.
    [Fact]
    public async Task Not_found_problem_carries_the_request_trace_id()
    {
        await using var factory = new ApiFactory();
        var traced = new TraceparentHandler(ActivityTraceId.CreateRandom());

        var response = await factory.CreateDefaultClient(traced).GetPathAsync($"/api/applications/{Guid.NewGuid()}");

        var problem = await response.AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.Contains(traced.TraceId, problem.GetProperty("traceId").GetString(), StringComparison.Ordinal);
    }

    // O7 (extends I40): a 400 ValidationProblemDetails carries the trace ID of the sent traceparent.
    [Fact]
    public async Task Validation_problem_carries_the_request_trace_id()
    {
        await using var factory = new ApiFactory();
        var traced = new TraceparentHandler(ActivityTraceId.CreateRandom());

        var response = await factory.CreateDefaultClient(traced).PostJsonAsync("/api/applications", Http.BodyWith("""{"termYears":4}"""));

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains(traced.TraceId, problem.GetProperty("traceId").GetString(), StringComparison.Ordinal);
    }

    // O8: a malformed body is a 400 that logs no error and no input value.
    [Fact]
    public async Task Malformed_body_logs_no_error_and_no_value()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostJsonAsync("/api/applications", """{"annualIncome":85000.00,"depositAmount":60000.00,""");

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Level >= LogLevel.Error);
        Assert.Empty(GoldenExample.Get("E1").InputsOnly.FindIn(factory.Logs.Entries.Select(e => e.Text)));
    }

    // O23 (P5-D5): EF Core command logs below Warning are not written. Control: a host configured at Information
    // writes them, so the check would see them.
    [Fact]
    public async Task Ef_command_logs_below_warning_are_not_written()
    {
        await using var control = new ApiFactory();
        using var informationHost = control.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [$"Logging:LogLevel:{_efCommand}"] = "Information" })));
        await informationHost.CreateClient().CreateApplicationAsync();
        Assert.Contains(control.Logs.Entries, e => e.Category == _efCommand && e.Level < LogLevel.Warning);
        await using var factory = new ApiFactory();

        await factory.CreateClient().CreateApplicationAsync();

        Assert.DoesNotContain(factory.Logs.Entries, e => e.Category == _efCommand && e.Level < LogLevel.Warning);
    }
}
