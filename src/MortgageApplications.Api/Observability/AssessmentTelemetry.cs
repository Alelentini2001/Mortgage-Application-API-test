using System.Diagnostics;
using System.Diagnostics.Metrics;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.Api.Observability;

/// <summary>
/// The worker's span and metrics (Phase 5). One <see cref="ActivitySource"/> and one <see cref="Meter"/>, both named
/// <see cref="Name"/>; the meter comes from <see cref="IMeterFactory"/>, so each host (and each test host) has its own.
/// Tags carry only the rules section 6 allowlist: the assessment and application IDs (span only), the outcome, the
/// decision and the rules version. Never an input, an output value, the Summary or the Checks.
/// </summary>
public sealed class AssessmentTelemetry : IDisposable
{
    /// <summary>The name of our activity source and meter (registered with <c>AddSource</c> and <c>AddMeter</c>).</summary>
    public const string Name = "MortgageApplications";

    public const string AssessmentIdTag = "assessment.id";
    public const string ApplicationIdTag = "application.id";
    public const string OutcomeTag = "assessment.outcome";
    public const string DecisionTag = "assessment.decision";
    public const string RulesVersionTag = "assessment.rules_version";

    // Seconds, the same boundaries ASP.NET Core uses for http.server.request.duration; the SDK default buckets are
    // meant for milliseconds and would put every assessment in the first one.
    private static readonly double[] _durationBuckets = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

    private readonly ActivitySource _source = new(Name);
    private readonly Counter<long> _processed;
    private readonly Histogram<double> _duration;

    public AssessmentTelemetry(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Name);
        _processed = meter.CreateCounter<long>(
            "mortgage.assessments.processed",
            unit: "{assessment}",
            description: "Assessments the worker finished as Completed or Failed.");
        _duration = meter.CreateHistogram(
            "mortgage.assessment.duration",
            unit: "s",
            description: "Time the worker took to process one assessment (load, calculate, save).",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = _durationBuckets });
    }

    /// <summary>
    /// Starts the <c>assessment.process</c> span (kind Internal) for one queued item. The caller makes it a root
    /// span (P5-D3 A: the POST that queued the item is a separate trace, linked by <see cref="AssessmentIdTag"/>).
    /// </summary>
    public Activity? StartProcessing(Guid assessmentId)
    {
        var activity = _source.StartActivity("assessment.process", ActivityKind.Internal);
        activity?.SetTag(AssessmentIdTag, assessmentId.ToString());
        return activity;
    }

    /// <summary>
    /// Tags the span with what processing did. Only Completed and Failed are counted and timed: NotFound and
    /// Skipped change nothing, so a re-queued item is never counted twice (Q5). The span status stays Unset: a
    /// Declined or Failed assessment is valid output, not an error (Q1).
    /// </summary>
    public void RecordProcessed(Activity? activity, AssessmentProcessingResult result, string rulesVersion, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.ApplicationId is { } applicationId)
        {
            activity?.SetTag(ApplicationIdTag, applicationId.ToString());
        }

        activity?.SetTag(OutcomeTag, result.Outcome.ToString());
        if (result.Outcome is not (AssessmentProcessingOutcome.Completed or AssessmentProcessingOutcome.Failed))
        {
            return;
        }

        var tags = new TagList { { OutcomeTag, result.Outcome.ToString() } };
        if (result.Decision is { } decision)
        {
            tags.Add(DecisionTag, decision.ToString());
        }

        tags.Add(RulesVersionTag, rulesVersion);
        foreach (var tag in tags)
        {
            activity?.SetTag(tag.Key, tag.Value);
        }

        _processed.Add(1, tags);
        _duration.Record(elapsed.TotalSeconds, tags);
    }

    /// <summary>
    /// A processing error (the item stays Pending): status Error with the exception type as the description. No
    /// message and no exception event, since a message could carry data; the worker's error log keeps the details.
    /// </summary>
    public static void RecordError(Activity? activity, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
    }

    public void Dispose() => _source.Dispose();
}
