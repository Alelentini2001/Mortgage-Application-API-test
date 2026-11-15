using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Every class that listens to spans or metrics runs in this collection, alone: <see cref="ActivityListener"/> and
/// <see cref="MeterListener"/> are process-wide (Phase 5 test proposal).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TelemetryGroup
{
    public const string Name = "Telemetry";
}

/// <summary>A finished span, copied when it stopped.</summary>
public sealed record CapturedSpan(
    string Source,
    string Name,
    ActivityKind Kind,
    string TraceId,
    string? ParentSpanId,
    ActivityStatusCode Status,
    string? StatusDescription,
    IReadOnlyList<KeyValuePair<string, string?>> Tags,
    IReadOnlyList<string> Events)
{
    public bool HasTag(string key) => Tags.Any(t => t.Key == key);

    public string? Tag(string key) => Tags.Where(t => t.Key == key).Select(t => t.Value).FirstOrDefault();

    /// <summary>Everything an exporter sends as text for this span: name, status description, tags and events.</summary>
    public string Text =>
        $"{Source} {Name} {StatusDescription} {string.Join(' ', Tags.Select(t => $"{t.Key}={t.Value}"))} {string.Join(' ', Events)}";
}

/// <summary>One metric measurement, copied when it was recorded.</summary>
public sealed record CapturedMeasurement(
    string Meter, string Instrument, string? Unit, double Value, IReadOnlyList<KeyValuePair<string, string?>> Tags)
{
    public bool HasTag(string key) => Tags.Any(t => t.Key == key);

    public string? Tag(string key) => Tags.Where(t => t.Key == key).Select(t => t.Value).FirstOrDefault();

    public string TagText => $"{Meter} {Instrument} {string.Join(' ', Tags.Select(t => $"{t.Key}={t.Value}"))}";
}

/// <summary>
/// Listens to our <c>MortgageApplications</c> source and the ASP.NET Core server spans, and to the metrics of one
/// host (instruments whose meter came from that host's <see cref="IMeterFactory"/>). Data is copied when a span
/// stops or a measurement is recorded. Start it after the host is built and before the requests under test.
/// </summary>
public sealed class TelemetryCapture : IDisposable
{
    private readonly ConcurrentQueue<CapturedSpan> _spans = new();
    private readonly ConcurrentQueue<CapturedMeasurement> _measurements = new();
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;

    public TelemetryCapture(IServiceProvider hostServices)
    {
        var meterScope = hostServices.GetRequiredService<IMeterFactory>();
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "MortgageApplications" or "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _spans.Enqueue(Copy(activity)),
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter.Scope, meterScope))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _meterListener.SetMeasurementEventCallback<long>((i, v, t, _) => Record(i, v, t));
        _meterListener.SetMeasurementEventCallback<int>((i, v, t, _) => Record(i, v, t));
        _meterListener.SetMeasurementEventCallback<double>((i, v, t, _) => Record(i, v, t));
        _meterListener.SetMeasurementEventCallback<float>((i, v, t, _) => Record(i, v, t));
        _meterListener.Start();
    }

    public IReadOnlyList<CapturedSpan> Spans => [.. _spans];

    public IReadOnlyList<CapturedMeasurement> Measurements => [.. _measurements];

    /// <summary>The worker's <c>assessment.process</c> spans for one assessment, in the order they stopped.</summary>
    public IReadOnlyList<CapturedSpan> ProcessSpans(Guid assessmentId) =>
        [.. Spans.Where(s => s.Source == "MortgageApplications" && s.Name == "assessment.process"
            && s.Tag("assessment.id") == assessmentId.ToString())];

    public IReadOnlyList<CapturedMeasurement> MeasurementsOf(string instrument) =>
        [.. Measurements.Where(m => m.Meter == "MortgageApplications" && m.Instrument == instrument)];

    /// <summary>Waits (bounded) for the first <c>assessment.process</c> span of an assessment.</summary>
    public async Task<CapturedSpan> WaitForProcessSpanAsync(Guid assessmentId)
    {
        await Http.EventuallyAsync(
            () => Task.FromResult(ProcessSpans(assessmentId).Count > 0),
            () => $"an assessment.process span for {assessmentId}");
        return ProcessSpans(assessmentId)[0];
    }

    /// <summary>Waits (bounded) for the first span that matches.</summary>
    public async Task<CapturedSpan> WaitForSpanAsync(Func<CapturedSpan, bool> match, string what)
    {
        await Http.EventuallyAsync(() => Task.FromResult(Spans.Any(match)), () => what);
        return Spans.First(match);
    }

    /// <summary>Waits (bounded) until our meter's instrument has at least <paramref name="count"/> measurements.</summary>
    public Task WaitForMeasurementsAsync(string instrument, int count) =>
        Http.EventuallyAsync(
            () => Task.FromResult(MeasurementsOf(instrument).Count >= count),
            () => $"{count} measurements of {instrument}; got {MeasurementsOf(instrument).Count}");

    public void Dispose()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
    }

    private static CapturedSpan Copy(Activity activity) => new(
        activity.Source.Name,
        activity.OperationName,
        activity.Kind,
        activity.TraceId.ToHexString(),
        activity.ParentSpanId == default ? null : activity.ParentSpanId.ToHexString(),
        activity.Status,
        activity.StatusDescription,
        [.. activity.TagObjects.Select(t => new KeyValuePair<string, string?>(t.Key, Convert.ToString(t.Value, CultureInfo.InvariantCulture)))],
        [.. activity.Events.Select(e => e.Name)]);

    private void Record<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        where T : struct, IConvertible
    {
        var copied = new List<KeyValuePair<string, string?>>(tags.Length);
        foreach (var tag in tags)
        {
            copied.Add(new KeyValuePair<string, string?>(tag.Key, Convert.ToString(tag.Value, CultureInfo.InvariantCulture)));
        }

        _measurements.Enqueue(new CapturedMeasurement(
            instrument.Meter.Name, instrument.Name, instrument.Unit, value.ToDouble(CultureInfo.InvariantCulture), copied));
    }
}

/// <summary>Sends a fixed W3C <c>traceparent</c> on every request, so the server spans of a test share its trace ID.</summary>
public sealed class TraceparentHandler(ActivityTraceId traceId) : DelegatingHandler
{
    public string TraceId { get; } = traceId.ToHexString();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("traceparent");
        request.Headers.TryAddWithoutValidation("traceparent", $"00-{TraceId}-{ActivitySpanId.CreateRandom().ToHexString()}-01");
        return base.SendAsync(request, cancellationToken);
    }
}
