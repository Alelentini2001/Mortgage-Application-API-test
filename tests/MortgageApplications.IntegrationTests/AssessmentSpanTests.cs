using System.Diagnostics;
using Microsoft.AspNetCore.TestHost;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 5 spans: one root <c>assessment.process</c> span (kind Internal) per processed item, with the allowlisted
/// tags; status Unset for every outcome (a Declined or Failed item is valid output), Error only for a processing
/// error, with the exception type and nothing else. The POST server span carries the assessment ID (P5-D3 A).
/// </summary>
[Collection(TelemetryGroup.Name)]
public sealed class AssessmentSpanTests
{
    // O10: Completed and Approved.
    [Fact]
    public async Task Approved_item_has_a_root_internal_span_with_ids_outcome_decision_and_rules_version()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        using var capture = new TelemetryCapture(factory.Services);

        var (applicationId, assessmentId, _) = await Telemetry.AssessAsync(client, Http.BaseBody);

        var span = await capture.WaitForProcessSpanAsync(assessmentId);
        Assert.Equal(ActivityKind.Internal, span.Kind);
        Assert.Null(span.ParentSpanId);
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Equal(applicationId, span.Tag("application.id"));
        Assert.Equal("Completed", span.Tag("assessment.outcome"));
        Assert.Equal("Approved", span.Tag("assessment.decision"));
        Assert.Equal("1.0", span.Tag("assessment.rules_version"));
        Assert.Empty(span.Events);
    }

    // O11: a Declined result is Completed, not an error.
    [Fact]
    public async Task Declined_item_has_a_completed_span_with_status_unset()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        using var capture = new TelemetryCapture(factory.Services);

        var (_, assessmentId, _) = await Telemetry.AssessAsync(client, GoldenExample.Get("E2").Body);

        var span = await capture.WaitForProcessSpanAsync(assessmentId);
        Assert.Equal("Completed", span.Tag("assessment.outcome"));
        Assert.Equal("Declined", span.Tag("assessment.decision"));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
    }

    // O12 (Q1, Q4): Failed has status Unset, the configured rules version and no decision tag.
    [Fact]
    public async Task Failed_item_has_a_span_without_decision_and_with_status_unset()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        using var capture = new TelemetryCapture(factory.Services);

        var (applicationId, assessmentId) = await Telemetry.AssessFailedAsync(factory.Services, client, Http.BaseBody);

        var span = await capture.WaitForProcessSpanAsync(assessmentId);
        Assert.Equal("Failed", span.Tag("assessment.outcome"));
        Assert.False(span.HasTag("assessment.decision"));
        Assert.Equal("1.0", span.Tag("assessment.rules_version"));
        Assert.Equal(applicationId, span.Tag("application.id"));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
    }

    // O9 (Q3): a processing error (a save that throws) sets status Error with the exception type only: no message,
    // no exception event, no outcome tag.
    [Fact]
    public async Task Processing_error_sets_status_error_with_the_exception_type_only()
    {
        await using var factory = new ApiFactory();
        await using var host = ErroringHost(factory);
        var client = host.CreateClient();
        using var capture = new TelemetryCapture(host.Services);

        var posted = await client.PostAssessmentAsync(await client.CreateApplicationAsync());

        var span = await capture.WaitForProcessSpanAsync(Guid.Parse(await posted.IdAsync()));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(nameof(InvalidOperationException), span.StatusDescription);
        Assert.Empty(span.Events);
        Assert.False(span.HasTag("assessment.outcome"));
    }

    // O13: after a processing error the worker goes on; the next item gets its own Completed span.
    [Fact]
    public async Task After_a_processing_error_the_next_item_has_its_own_completed_span()
    {
        await using var factory = new ApiFactory();
        await using var host = ErroringHost(factory);
        var client = host.CreateClient();
        using var capture = new TelemetryCapture(host.Services);
        var errored = Guid.Parse(await (await client.PostAssessmentAsync(await client.CreateApplicationAsync())).IdAsync());
        await capture.WaitForProcessSpanAsync(errored);

        var (_, next, _) = await Telemetry.AssessAsync(client, Http.BaseBody);

        var span = await capture.WaitForProcessSpanAsync(next);
        Assert.Equal("Completed", span.Tag("assessment.outcome"));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Single(capture.ProcessSpans(errored));
    }

    // O14 (P5-D3 A): exactly one span per processed item, in its own trace, not the POST's.
    [Fact]
    public async Task Processed_item_has_exactly_one_span_in_a_trace_separate_from_the_post()
    {
        await using var factory = new ApiFactory();
        var traced = new TraceparentHandler(ActivityTraceId.CreateRandom());
        var client = factory.CreateDefaultClient(traced);
        using var capture = new TelemetryCapture(factory.Services);

        var (_, assessmentId, _) = await Telemetry.AssessAsync(client, Http.BaseBody);
        await capture.WaitForProcessSpanAsync(assessmentId);
        var (_, marker, _) = await Telemetry.AssessAsync(client, Http.BaseBody);
        await capture.WaitForProcessSpanAsync(marker);

        var span = Assert.Single(capture.ProcessSpans(assessmentId));
        Assert.NotEqual(traced.TraceId, span.TraceId);
        Assert.Null(span.ParentSpanId);
    }

    // O15 (Q2): the POST that queued the item tags its server span with the assessment ID.
    [Fact]
    public async Task Post_assessment_server_span_carries_the_assessment_id()
    {
        await using var factory = new ApiFactory();
        var traced = new TraceparentHandler(ActivityTraceId.CreateRandom());
        var client = factory.CreateDefaultClient(traced);
        using var capture = new TelemetryCapture(factory.Services);

        var posted = await client.PostAssessmentAsync(await client.CreateApplicationAsync());

        var assessmentId = await posted.IdAsync();
        var span = await capture.WaitForSpanAsync(
            s => s.Kind == ActivityKind.Server && s.Tag("assessment.id") == assessmentId,
            $"a server span tagged with {assessmentId}");
        Assert.Equal("Microsoft.AspNetCore", span.Source);
        Assert.Equal(traced.TraceId, span.TraceId);
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> ErroringHost(ApiFactory factory) =>
        factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(services => services.FailFirstSave(new FailFirstSaveState())),
            runWorker: true);
}
