using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Holds the worker at a <see cref="WorkerGatePoint"/> of its first item until the test opens the gate, so a test
/// can observe Pending (or a full queue) without a sleep. The wait honours the worker's token, so a host can still
/// stop while held.
/// </summary>
public sealed class WorkerGate
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the worker has reached the gate point and is held.</summary>
    public Task Entered => _entered.Task;

    public void Open() => _open.TrySetResult();

    public async Task PassAsync(CancellationToken cancellationToken)
    {
        _entered.TrySetResult();
        await _open.Task.WaitAsync(cancellationToken);
    }
}

public enum WorkerGatePoint
{
    /// <summary>Before the processing load: the item is off the queue and nothing is in memory yet.</summary>
    BeforeLoad,

    /// <summary>Before the save: the assessment is already Completed or Failed in memory, not in the database.</summary>
    BeforeSave,
}

public static class WorkerGateServices
{
    /// <summary>Wraps the app's repository so the worker waits at the gate at <paramref name="point"/>.</summary>
    public static IServiceCollection HoldWorkerAt(
        this IServiceCollection services, WorkerGate gate, WorkerGatePoint point = WorkerGatePoint.BeforeLoad) =>
        services.DecorateRepository(inner => new GatedRepository(inner, gate, point));

    /// <summary>Wraps the app's repository so the first <c>UpdateAssessmentAsync</c> throws (a failed save).</summary>
    public static IServiceCollection FailFirstSave(this IServiceCollection services, FailFirstSaveState state) =>
        services.DecorateRepository(inner => new FailFirstSaveRepository(inner, state));

    private static IServiceCollection DecorateRepository(
        this IServiceCollection services, Func<IMortgageApplicationRepository, IMortgageApplicationRepository> decorate)
    {
        var original = services.Single(d => d.ServiceType == typeof(IMortgageApplicationRepository));
        services.Remove(original);
        services.AddScoped(sp => decorate(
            (IMortgageApplicationRepository)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!)));
        return services;
    }

    private sealed class GatedRepository(IMortgageApplicationRepository inner, WorkerGate gate, WorkerGatePoint point)
        : DelegatingRepository(inner)
    {
        public override async Task<AssessmentWorkItem?> FindForProcessingAsync(Guid assessmentId, CancellationToken cancellationToken)
        {
            if (point == WorkerGatePoint.BeforeLoad)
            {
                await gate.PassAsync(cancellationToken);
            }

            return await base.FindForProcessingAsync(assessmentId, cancellationToken);
        }

        public override async Task UpdateAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken)
        {
            if (point == WorkerGatePoint.BeforeSave)
            {
                await gate.PassAsync(cancellationToken);
            }

            await base.UpdateAssessmentAsync(assessment, cancellationToken);
        }
    }

    private sealed class FailFirstSaveRepository(IMortgageApplicationRepository inner, FailFirstSaveState state)
        : DelegatingRepository(inner)
    {
        public override Task UpdateAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken) =>
            state.TryTakeFailure()
                ? throw new InvalidOperationException("Simulated save failure.")
                : base.UpdateAssessmentAsync(assessment, cancellationToken);
    }

    private abstract class DelegatingRepository(IMortgageApplicationRepository inner) : IMortgageApplicationRepository
    {
        public Task AddAsync(MortgageApplication application, CancellationToken cancellationToken) =>
            inner.AddAsync(application, cancellationToken);

        public Task<MortgageApplication?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
            inner.FindAsync(id, ownerId, cancellationToken);

        public Task AddAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken) =>
            inner.AddAssessmentAsync(assessment, cancellationToken);

        public Task<AffordabilityAssessment?> FindAssessmentAsync(
            Guid applicationId, Guid assessmentId, string ownerId, CancellationToken cancellationToken) =>
            inner.FindAssessmentAsync(applicationId, assessmentId, ownerId, cancellationToken);

        public virtual Task<AssessmentWorkItem?> FindForProcessingAsync(Guid assessmentId, CancellationToken cancellationToken) =>
            inner.FindForProcessingAsync(assessmentId, cancellationToken);

        public virtual Task UpdateAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken) =>
            inner.UpdateAssessmentAsync(assessment, cancellationToken);

        public Task<IReadOnlyList<Guid>> ListPendingAssessmentIdsAsync(CancellationToken cancellationToken) =>
            inner.ListPendingAssessmentIdsAsync(cancellationToken);
    }
}

/// <summary>Shared by every scope of one host: exactly one save fails.</summary>
public sealed class FailFirstSaveState
{
    private int _failed;

    public bool TryTakeFailure() => Interlocked.Exchange(ref _failed, 1) == 0;
}
