using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.Api.Assessments;

/// <summary>
/// The in-process queue (singleton): a bounded channel of assessment IDs with one reader, the
/// <see cref="AssessmentWorker"/>. When it is full, <see cref="EnqueueAsync"/> waits for room
/// (<see cref="BoundedChannelFullMode.Wait"/>, P6-D2) instead of dropping an ID, bounded by the request's token.
/// IDs in memory are lost on a restart, but their rows are still Pending and the worker re-enqueues them at startup.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "It is a queue; the name was approved in the Phase 6 plan (P6-D1).")]
public sealed class ChannelAssessmentQueue : IAssessmentQueue
{
    private readonly Channel<Guid> _channel;
    private readonly TimeSpan _enqueueTimeout;

    public ChannelAssessmentQueue(IOptions<AssessmentQueueOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _enqueueTimeout = TimeSpan.FromSeconds(options.Value.EnqueueTimeoutSeconds);
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
    }

    /// <summary>For the worker (the only reader).</summary>
    public ChannelReader<Guid> Reader => _channel.Reader;

    // TryWrite first: WriteAsync returns cancelled at once for a token that is already cancelled, even when there is
    // room, which would leave the saved row Pending until a restart. The token and the enqueue timeout only bound a
    // wait for room (P8-D2 A).
    public ValueTask EnqueueAsync(Guid assessmentId, CancellationToken cancellationToken) =>
        _channel.Writer.TryWrite(assessmentId)
            ? ValueTask.CompletedTask
            : EnqueueSlowAsync(assessmentId, cancellationToken);

    private async ValueTask EnqueueSlowAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(_enqueueTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await _channel.Writer.WriteAsync(assessmentId, linked.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for room in the assessment queue.");
        }
    }
}
