using System.Diagnostics.CodeAnalysis;

namespace MortgageApplications.Core.Applications;

/// <summary>
/// Hands a saved Pending assessment to the background worker (P6-D1). The use case saves first and enqueues
/// second, so a crash in between leaves a Pending row that the worker re-enqueues at its next start.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "It is a queue; the name was approved in the Phase 6 plan (P6-D1).")]
public interface IAssessmentQueue
{
    /// <summary>Waits while the queue is full (P6-D2): an ID is never dropped.</summary>
    /// <exception cref="OperationCanceledException">The caller gave up while waiting; the assessment stays Pending.</exception>
    ValueTask EnqueueAsync(Guid assessmentId, CancellationToken cancellationToken);
}
