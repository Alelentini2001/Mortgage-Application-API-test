using MortgageApplications.Core.Applications;

namespace MortgageApplications.Api.Assessments;

/// <summary>
/// Processes queued assessments one at a time, each in its own DI scope (the DbContext is scoped; this service is a
/// singleton). At startup it re-enqueues every Pending assessment, so nothing saved before a restart is lost.
/// <para>
/// An exception escaping <see cref="ExecuteAsync"/> stops the host (.NET 6+ default), so every item is caught and
/// logged, and only shutdown leaves the loop. Shutdown is never a failure: an unfinished item stays Pending and is
/// picked up at the next start. Logs carry the assessment ID only: never amounts, inputs or the owner ID.
/// </para>
/// </summary>
public sealed partial class AssessmentWorker(
    ChannelAssessmentQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<AssessmentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The re-enqueue runs next to the read loop, not before it: with more Pending rows than the queue holds,
        // writing them all before reading would wait forever.
        var requeue = RequeuePendingAsync(stoppingToken);
        await ProcessQueueAsync(stoppingToken);
        await requeue;
    }

    private async Task RequeuePendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            IReadOnlyList<Guid> pending;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                pending = await scope.ServiceProvider.GetRequiredService<MortgageApplicationService>()
                    .ListPendingAssessmentIdsAsync(stoppingToken);
            }

            foreach (var assessmentId in pending)
            {
                await queue.EnqueueAsync(assessmentId, stoppingToken);
            }

            LogPendingRequeued(pending.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown during startup: the rows are still Pending for the next start.
        }
        catch (Exception exception)
        {
            LogRequeueFailed(exception);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var assessmentId in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessOneAsync(assessmentId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown: leave the loop. An item in progress was not saved, so it stays Pending.
        }
    }

    private async Task ProcessOneAsync(Guid assessmentId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<MortgageApplicationService>()
                .ProcessAssessmentAsync(assessmentId, stoppingToken);
            switch (outcome)
            {
                case AssessmentProcessingOutcome.Failed:
                    LogAssessmentFailed(assessmentId);
                    break;
                case AssessmentProcessingOutcome.NotFound:
                    LogAssessmentNotFound(assessmentId);
                    break;
                default:
                    LogAssessmentProcessed(assessmentId, outcome);
                    break;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogProcessingError(assessmentId, exception);
        }
    }

    [LoggerMessage(EventId = 1, EventName = "PendingRequeued", Level = LogLevel.Information,
        Message = "Re-enqueued {Count} pending assessments at startup.")]
    private partial void LogPendingRequeued(int count);

    [LoggerMessage(EventId = 2, EventName = "RequeueFailed", Level = LogLevel.Error,
        Message = "Re-enqueueing pending assessments at startup failed; they stay Pending until the next start.")]
    private partial void LogRequeueFailed(Exception exception);

    [LoggerMessage(EventId = 3, EventName = "AssessmentProcessed", Level = LogLevel.Debug,
        Message = "Assessment {AssessmentId} processed: {Outcome}.")]
    private partial void LogAssessmentProcessed(Guid assessmentId, AssessmentProcessingOutcome outcome);

    [LoggerMessage(EventId = 4, EventName = "AssessmentFailed", Level = LogLevel.Warning,
        Message = "Assessment {AssessmentId} was marked Failed: the calculation threw an error.")]
    private partial void LogAssessmentFailed(Guid assessmentId);

    [LoggerMessage(EventId = 5, EventName = "AssessmentNotFound", Level = LogLevel.Warning,
        Message = "Assessment {AssessmentId} or its application was not found; nothing was changed.")]
    private partial void LogAssessmentNotFound(Guid assessmentId);

    [LoggerMessage(EventId = 6, EventName = "ProcessingError", Level = LogLevel.Error,
        Message = "Processing assessment {AssessmentId} failed; it stays Pending until the next start.")]
    private partial void LogProcessingError(Guid assessmentId, Exception exception);
}
