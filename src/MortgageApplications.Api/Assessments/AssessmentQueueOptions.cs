using System.ComponentModel.DataAnnotations;

namespace MortgageApplications.Api.Assessments;

/// <summary>The <c>Assessments</c> configuration section, validated at startup (Q7).</summary>
public sealed class AssessmentQueueOptions
{
    public const string SectionName = "Assessments";

    /// <summary>How many assessment IDs can wait in memory; a POST waits for room when the queue is full (P6-D2).</summary>
    [Range(1, 10_000)]
    public int QueueCapacity { get; set; } = 100;

    /// <summary>How long the worker waits before it retries a failed startup re-enqueue (R3).</summary>
    [Range(1, 3_600)]
    public int RequeueRetryDelaySeconds { get; set; } = 30;

    /// <summary>How long a POST waits on a full queue before returning 503 Service Unavailable (P8-D2 A).</summary>
    [Range(1, 300)]
    public int EnqueueTimeoutSeconds { get; set; } = 10;
}
