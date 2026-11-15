using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 5 metrics: counter <c>mortgage.assessments.processed</c> and histogram <c>mortgage.assessment.duration</c>
/// (seconds), recorded once per Completed or Failed item only (Q3, Q5), with the outcome, decision (Completed only)
/// and rules version. Measurements are filtered to the test host's own meter factory.
/// </summary>
[Collection(TelemetryGroup.Name)]
public sealed class AssessmentMetricTests
{
    // O16: one count per item, tagged by outcome and decision; the histogram has the same tags.
    [Fact]
    public async Task Counter_and_histogram_are_recorded_per_outcome_and_decision()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        using var capture = new TelemetryCapture(factory.Services);

        await Telemetry.AssessAsync(client, Http.BaseBody);
        await Telemetry.AssessAsync(client, GoldenExample.Get("E2").Body);
        await Telemetry.AssessFailedAsync(factory.Services, client, Http.BaseBody);
        await capture.WaitForMeasurementsAsync(Telemetry.Counter, 3);
        await capture.WaitForMeasurementsAsync(Telemetry.Histogram, 3);

        string[] expected = ["Completed/Approved/1.0", "Completed/Declined/1.0", "Failed/-/1.0"];
        var counts = capture.MeasurementsOf(Telemetry.Counter);
        Assert.All(counts, m => Assert.Equal(1, m.Value));
        Assert.Equal(expected, counts.Select(Tags).Order(StringComparer.Ordinal));
        Assert.Equal(expected, capture.MeasurementsOf(Telemetry.Histogram).Select(Tags).Order(StringComparer.Ordinal));
    }

    // O17 (Q13): the duration is the worker's processing time from the injected TimeProvider, in seconds.
    [Fact]
    public async Task Histogram_records_the_processing_time_in_seconds_from_the_time_provider()
    {
        await using var factory = new ApiFactory();
        await using var host = factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(new SteppingTimeProvider(TimeSpan.FromMilliseconds(1500)))),
            runWorker: true);
        var client = host.CreateClient();
        using var capture = new TelemetryCapture(host.Services);

        await Telemetry.AssessAsync(client, Http.BaseBody);
        await capture.WaitForMeasurementsAsync(Telemetry.Histogram, 1);

        var duration = Assert.Single(capture.MeasurementsOf(Telemetry.Histogram));
        Assert.Equal("s", duration.Unit);
        Assert.Equal(1.5, duration.Value);
        Assert.Equal("Completed/Approved/1.0", Tags(duration));
    }

    // O18 (Q5): a re-queued ID gets a Skipped span and an unknown ID a NotFound span (no application ID); neither is
    // counted or timed.
    [Fact]
    public async Task Requeued_and_unknown_ids_get_spans_but_are_not_counted()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        using var capture = new TelemetryCapture(factory.Services);
        var (applicationId, assessmentId, _) = await Telemetry.AssessAsync(client, Http.BaseBody);
        await capture.WaitForProcessSpanAsync(assessmentId);
        var unknown = Guid.NewGuid();
        var queue = factory.Services.GetRequiredService<IAssessmentQueue>();

        await queue.EnqueueAsync(assessmentId, Http.Ct);
        await queue.EnqueueAsync(unknown, Http.Ct);

        // One item at a time, in order: once a later item has its span, both IDs have been handled.
        var (_, marker, _) = await Telemetry.AssessAsync(client, Http.BaseBody);
        await capture.WaitForProcessSpanAsync(marker);
        Assert.Equal(["Completed", "Skipped"], capture.ProcessSpans(assessmentId).Select(s => s.Tag("assessment.outcome")));
        var skipped = capture.ProcessSpans(assessmentId)[1];
        Assert.Equal(applicationId, skipped.Tag("application.id"));
        Assert.False(skipped.HasTag("assessment.decision"));
        var notFound = Assert.Single(capture.ProcessSpans(unknown));
        Assert.Equal("NotFound", notFound.Tag("assessment.outcome"));
        Assert.False(notFound.HasTag("application.id"));
        Assert.Equal(2, capture.MeasurementsOf(Telemetry.Counter).Count);
        Assert.Equal(2, capture.MeasurementsOf(Telemetry.Histogram).Count);
    }

    // Security gate (instrument set) and review gate (O17 advice): the MortgageApplications meter publishes exactly
    // the two allowed instruments, with their types and units, so a new instrument recording an amount cannot slip
    // past the tag checks; the histogram advises second-based bucket boundaries (Instrument<T>.Advice, public in .NET 9+).
    [Fact]
    public async Task Our_meter_publishes_only_the_two_allowed_instruments_with_second_buckets()
    {
        await using var factory = new ApiFactory();
        _ = factory.CreateClient();

        using var capture = new TelemetryCapture(factory.Services);

        var ours = capture.Published.Where(p => p.Meter == "MortgageApplications").OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
        Assert.Equal(
            [
                (Telemetry.Histogram, typeof(Histogram<double>), "s"),
                (Telemetry.Counter, typeof(Counter<long>), "{assessment}"),
            ],
            ours.Select(p => (p.Name, p.Type, p.Unit)));
        Assert.Equal(_durationBuckets, ours[0].BucketBoundaries);
    }

    private static readonly double[] _durationBuckets = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

    private static string Tags(CapturedMeasurement m) =>
        $"{m.Tag("assessment.outcome")}/{m.Tag("assessment.decision") ?? "-"}/{m.Tag("assessment.rules_version")}";

    /// <summary>Every timestamp read is one step after the previous one, so an elapsed time is exactly one step.</summary>
    private sealed class SteppingTimeProvider(TimeSpan step) : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Add(ref _ticks, step.Ticks);
    }
}
