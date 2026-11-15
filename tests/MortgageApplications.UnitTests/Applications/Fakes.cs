using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.UnitTests.Applications;

/// <summary>
/// In-memory repository with the same owner rules as the EF one (every caller read filters on the owner).
/// The processing methods are not owner-scoped, as in the EF one.
/// </summary>
internal sealed class FakeRepository : IMortgageApplicationRepository
{
    public List<MortgageApplication> Applications { get; } = [];

    public List<AffordabilityAssessment> Assessments { get; } = [];

    /// <summary>Every assessment passed to <see cref="UpdateAssessmentAsync"/>, in call order.</summary>
    public List<AffordabilityAssessment> Updates { get; } = [];

    /// <summary>
    /// When set, the processing load returns these inputs instead of the application's: a row written before a
    /// rules change, which <see cref="MortgageApplication.Create"/> could not build.
    /// </summary>
    public AffordabilityInputs? StoredInputsOverride { get; set; }

    /// <summary>When set, <see cref="UpdateAssessmentAsync"/> throws it (a failed save).</summary>
    public Exception? UpdateFailure { get; set; }

    public Task AddAsync(MortgageApplication application, CancellationToken cancellationToken)
    {
        Applications.Add(application);
        return Task.CompletedTask;
    }

    public Task<MortgageApplication?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(Applications.SingleOrDefault(a => a.Id == id && a.OwnerId == ownerId));

    public Task AddAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken)
    {
        Assessments.Add(assessment);
        return Task.CompletedTask;
    }

    public Task<AffordabilityAssessment?> FindAssessmentAsync(
        Guid applicationId, Guid assessmentId, string ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(Assessments.SingleOrDefault(s =>
            s.Id == assessmentId
            && s.ApplicationId == applicationId
            && Applications.Any(a => a.Id == applicationId && a.OwnerId == ownerId)));

    public Task<AssessmentWorkItem?> FindForProcessingAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        var assessment = Assessments.SingleOrDefault(s => s.Id == assessmentId);
        var application = Applications.SingleOrDefault(a => a.Id == assessment?.ApplicationId);
        return Task.FromResult(assessment is null || application is null
            ? null
            : new AssessmentWorkItem(assessment, StoredInputsOverride ?? application.ToInputs()));
    }

    public Task UpdateAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken)
    {
        if (UpdateFailure is not null)
        {
            throw UpdateFailure;
        }

        Updates.Add(assessment);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> ListPendingAssessmentIdsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            [.. Assessments.Where(s => s.Status == AssessmentStatus.Pending).Select(s => s.Id)]);
}

/// <summary>Records every enqueued ID; <paramref name="onEnqueue"/> runs at the moment of the call.</summary>
internal sealed class FakeAssessmentQueue(Action<Guid>? onEnqueue = null) : IAssessmentQueue
{
    public List<Guid> Enqueued { get; } = [];

    public ValueTask EnqueueAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        onEnqueue?.Invoke(assessmentId);
        Enqueued.Add(assessmentId);
        return ValueTask.CompletedTask;
    }
}

/// <summary>A clock that stays at one UTC instant until a test moves it (A11: no FakeTimeProvider package).</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
