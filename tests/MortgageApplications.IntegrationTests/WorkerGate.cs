using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Holds the worker at the start of its first item until the test opens the gate, so a test can observe Pending
/// (or a full queue) without a sleep. The wait honours the worker's token, so a host can still stop while held.
/// </summary>
public sealed class WorkerGate
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the worker has taken an item off the queue and is held.</summary>
    public Task Entered => _entered.Task;

    public void Open() => _open.TrySetResult();

    public async Task PassAsync(CancellationToken cancellationToken)
    {
        _entered.TrySetResult();
        await _open.Task.WaitAsync(cancellationToken);
    }
}

public static class WorkerGateServices
{
    /// <summary>Wraps the app's repository so the worker's processing load waits at the gate.</summary>
    public static IServiceCollection HoldWorkerAt(this IServiceCollection services, WorkerGate gate)
    {
        var original = services.Single(d => d.ServiceType == typeof(IMortgageApplicationRepository));
        services.Remove(original);
        services.AddScoped<IMortgageApplicationRepository>(sp => new GatedRepository(
            (IMortgageApplicationRepository)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!), gate));
        return services;
    }

    private sealed class GatedRepository(IMortgageApplicationRepository inner, WorkerGate gate) : IMortgageApplicationRepository
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

        public async Task<AssessmentWorkItem?> FindForProcessingAsync(Guid assessmentId, CancellationToken cancellationToken)
        {
            await gate.PassAsync(cancellationToken);
            return await inner.FindForProcessingAsync(assessmentId, cancellationToken);
        }

        public Task UpdateAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken) =>
            inner.UpdateAssessmentAsync(assessment, cancellationToken);

        public Task<IReadOnlyList<Guid>> ListPendingAssessmentIdsAsync(CancellationToken cancellationToken) =>
            inner.ListPendingAssessmentIdsAsync(cancellationToken);
    }
}
