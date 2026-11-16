using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 5 allowlist (rules section 6): no input or output number, raw or formatted, and no Summary text reaches the
/// captured logs (message, exception, state, scopes), span names, tags, status or events, or metric tags; and our own
/// tag and log field names are limited to the allowlist.
/// </summary>
[Collection(TelemetryGroup.Name)]
public sealed class TelemetryAllowlistTests
{
    private static readonly string[] _spanKeys =
        ["assessment.id", "application.id", "assessment.outcome", "assessment.decision", "assessment.rules_version"];

    private static readonly string[] _metricKeys = ["assessment.outcome", "assessment.decision", "assessment.rules_version"];

    private static readonly string[] _logKeys =
        ["AssessmentId", "ApplicationId", "Outcome", "Count", "RetryDelaySeconds", "{OriginalFormat}"];

    // What the ASP.NET Core instrumentation (1.20.1) sets on our server spans today, minus the client-controlled
    // user_agent.original, plus our assessment.id. Rules section 6 does not name these HTTP keys yet (see the phase
    // file, "Open issues").
    private static readonly string[] _serverSpanKeys =
    [
        "error.type", "http.request.method", "http.response.status_code", "http.route", "network.protocol.version",
        "server.address", "url.path", "url.query", "url.scheme", "assessment.id",
    ];

    public static TheoryData<string> GoldenCases => ["E1", "E2"];

    // O1 (E1, Approved) and O2 (E2, Declined): every form of every input and output number, and the Summary, are
    // absent from logs, span text and metric tags. Control: the same search finds the loan in the GET body.
    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task Assessment_numbers_reach_no_log_span_or_metric_tag(string golden)
    {
        var example = GoldenExample.Get(golden);
        await using var factory = new ApiFactory();
        var traced = new TraceparentHandler(ActivityTraceId.CreateRandom());
        var client = factory.CreateDefaultClient(traced);
        using var capture = new TelemetryCapture(factory.Services);

        var (_, assessmentId, body) = await Telemetry.AssessAsync(client, example.Body);
        await capture.WaitForProcessSpanAsync(assessmentId);
        await capture.WaitForMeasurementsAsync(Telemetry.Counter, 1);

        Assert.Contains(example.LoanF2, example.Forbidden.Find(body));
        var spans = capture.Spans
            .Where(s => s.TraceId == traced.TraceId || s.Tag("assessment.id") == assessmentId.ToString())
            .ToList();
        Assert.Contains(spans, s => s.Kind == ActivityKind.Server);
        Assert.Empty(example.Forbidden.FindIn(spans.Select(s => s.Text)));
        Assert.Empty(example.Forbidden.FindIn(capture.Measurements.Select(m => m.TagText)));
        Assert.Empty(example.Forbidden.FindIn(factory.Logs.Entries.Select(e => e.Text)));
    }

    // O3: our span tag keys and metric tag keys are limited to the allowlist, for a Completed and a Failed item.
    [Fact]
    public async Task Our_span_and_metric_tag_keys_are_in_the_allowlist()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        using var capture = new TelemetryCapture(factory.Services);

        var (_, completed, _) = await Telemetry.AssessAsync(client, Http.BaseBody);
        var (_, failed) = await Telemetry.AssessFailedAsync(factory.Services, client, Http.BaseBody);
        await capture.WaitForProcessSpanAsync(completed);
        await capture.WaitForProcessSpanAsync(failed);
        await capture.WaitForMeasurementsAsync(Telemetry.Counter, 2);

        var ourSpans = capture.Spans.Where(s => s.Source == "MortgageApplications").ToList();
        var ourMeasurements = capture.Measurements.Where(m => m.Meter == "MortgageApplications").ToList();
        Assert.NotEmpty(ourMeasurements);
        Assert.All(ourSpans.SelectMany(s => s.Tags), t => Assert.Contains(t.Key, _spanKeys));
        Assert.All(ourMeasurements.SelectMany(m => m.Tags), t => Assert.Contains(t.Key, _metricKeys));
    }

    // O5 (extends I-13, I-14 and review R1 to spans, metrics, log state and scopes): a Failed item and a processing
    // error with sentinel inputs leave none of their digits, nor the owner ID, in any captured telemetry.
    [Fact]
    public async Task Failed_and_errored_items_leave_no_inputs_or_owner_in_telemetry()
    {
        await using var factory = new ApiFactory();
        await using var host = factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(services => services.FailFirstSave(new FailFirstSaveState())),
            runWorker: true);
        var client = host.CreateClient();
        using var capture = new TelemetryCapture(host.Services);

        var errored = await client.PostAssessmentAsync(await client.CreateApplicationAsync(Telemetry.SentinelBody));
        await capture.WaitForProcessSpanAsync(Guid.Parse(await errored.IdAsync()));
        var (_, failed) = await Telemetry.AssessFailedAsync(host.Services, client, Telemetry.SentinelBody);
        await capture.WaitForProcessSpanAsync(failed);

        Assert.Contains(factory.Logs.Entries, e => e.EventName == "ProcessingError");
        const string owner = ApiFactory.DefaultOwner;
        var clean = new ForbiddenNumbers([], []);
        var texts = capture.Spans.Select(s => s.Text)
            .Concat(capture.Measurements.Select(m => m.TagText))
            .Concat(factory.Logs.Entries.Select(e => e.Text))
            .Select(clean.Sanitize)
            .ToList();
        foreach (var sentinel in new[] { "85123", "912.34", "61234", "301234", owner })
        {
            Assert.DoesNotContain(texts, t => t.Contains(sentinel, StringComparison.Ordinal));
        }
    }

    // O4 (security and review gates; skipped in the proposal): the framework's server spans carry only the
    // allowlisted keys, for a 201, a 202 with a query string, a 200, a 404 and a 500, all sent with a User-Agent.
    [Fact]
    public async Task Server_span_tag_keys_are_in_the_allowlist()
    {
        await using var factory = new ApiFactory();
        // One handler per client: a DelegatingHandler belongs to one pipeline. Both send the same trace ID.
        var traceId = ActivityTraceId.CreateRandom();
        var traced = new TraceparentHandler(traceId);
        var client = factory.CreateDefaultClient(traced);
        using var throwing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddScoped<IMortgageApplicationRepository, ValueFreeThrowingRepository>()));
        var throwingClient = throwing.CreateDefaultClient(new TraceparentHandler(traceId));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("probe/85123");
        throwingClient.DefaultRequestHeaders.UserAgent.ParseAdd("probe/85123");
        using var capture = new TelemetryCapture(factory.Services);

        var applicationId = await client.CreateApplicationAsync();
        var posted = await client.PostJsonAsync($"/api/applications/{applicationId}/assessments?x=1", "");
        await client.GetPathAsync(posted.Headers.Location!.OriginalString);
        await client.GetPathAsync($"/api/applications/{Guid.NewGuid()}");
        await throwingClient.GetPathAsync($"/api/applications/{Guid.NewGuid()}");

        Func<CapturedSpan, bool> ours = s => s.TraceId == traced.TraceId && s.Kind == ActivityKind.Server;
        await Http.EventuallyAsync(() => Task.FromResult(capture.Spans.Count(ours) >= 5), () => "the 5 server spans");
        var keys = capture.Spans.Where(ours).SelectMany(s => s.Tags).Select(t => t.Key).Distinct().ToList();
        Assert.Empty(keys.Except(_serverSpanKeys));
        Assert.Empty(_serverSpanKeys.Except(keys));
    }

    // O22: our own log entries carry only allowlisted field names. Control: the new queued log is among them.
    [Fact]
    public async Task Our_log_field_names_are_in_the_allowlist()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        await Telemetry.AssessAsync(client, Http.BaseBody);
        await Telemetry.AssessFailedAsync(factory.Services, client, Http.BaseBody);

        var ours = factory.Logs.Entries.Where(e => e.Category.StartsWith("MortgageApplications.", StringComparison.Ordinal)).ToList();
        Assert.Contains(ours, e => e.EventName == "AssessmentQueued");
        Assert.All(ours.SelectMany(e => e.State), kv => Assert.Contains(kv.Key, _logKeys));
    }

    // Q12 (nice): the API has no query parameters, but a client can send one; its value reaches no span.
    [Fact]
    public async Task Query_string_value_reaches_no_span()
    {
        await using var factory = new ApiFactory();
        var traced = new TraceparentHandler(ActivityTraceId.CreateRandom());
        var client = factory.CreateDefaultClient(traced);
        using var capture = new TelemetryCapture(factory.Services);
        var applicationId = await client.CreateApplicationAsync();

        var response = await client.GetPathAsync($"/api/applications/{applicationId}?x=85123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Http.EventuallyAsync(
            () => Task.FromResult(capture.Spans.Count(s => s.TraceId == traced.TraceId && s.Kind == ActivityKind.Server) >= 2),
            () => "the server spans of the POST and the GET");
        Assert.DoesNotContain(
            capture.Spans.Where(s => s.TraceId == traced.TraceId),
            s => s.Text.Contains("85123", StringComparison.Ordinal));
    }
}
