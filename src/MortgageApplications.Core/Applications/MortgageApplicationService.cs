using MortgageApplications.Core.Affordability;

namespace MortgageApplications.Core.Applications;

/// <summary>
/// The use cases: create, get, assess and get an assessment, plus processing a queued assessment (background worker).
/// The owner always comes from the caller (the current user), never from request data; "not found" and "another
/// owner's" both return <c>null</c>. Times come from the injected <see cref="TimeProvider"/>, so the calculator stays pure.
/// </summary>
public sealed class MortgageApplicationService(
    IMortgageApplicationRepository repository,
    AffordabilityCalculator calculator,
    IAssessmentQueue queue,
    TimeProvider timeProvider)
{
    /// <exception cref="ArgumentOutOfRangeException">An input breaks a rules section 1 precondition (the API validates first).</exception>
    public async Task<MortgageApplication> CreateAsync(
        string ownerId, AffordabilityInputs inputs, CancellationToken cancellationToken)
    {
        var application = MortgageApplication.Create(ownerId, inputs, timeProvider.GetUtcNow());
        await repository.AddAsync(application, cancellationToken);
        return application;
    }

    public Task<MortgageApplication?> GetAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        repository.FindAsync(id, ownerId, cancellationToken);

    /// <summary>
    /// Saves a Pending assessment, then puts its ID on the queue for the background worker, in that order: if the
    /// process stops in between, the saved row is still Pending and the worker re-enqueues it at its next start.
    /// </summary>
    /// <exception cref="OperationCanceledException">The caller gave up while the queue was full; the assessment stays Pending.</exception>
    public async Task<AffordabilityAssessment?> AssessAsync(
        string ownerId, Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await repository.FindAsync(applicationId, ownerId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var assessment = AffordabilityAssessment.CreatePending(application.Id, timeProvider.GetUtcNow());
        await repository.AddAssessmentAsync(assessment, cancellationToken);
        await queue.EnqueueAsync(assessment.Id, cancellationToken);
        return assessment;
    }

    public Task<AffordabilityAssessment?> GetAssessmentAsync(
        string ownerId, Guid applicationId, Guid assessmentId, CancellationToken cancellationToken) =>
        repository.FindAssessmentAsync(applicationId, assessmentId, ownerId, cancellationToken);

    /// <summary>
    /// Runs one queued assessment. For the background worker only: it is not owner-scoped, because the ID comes from
    /// our own queue, never from a request. Idempotent: an assessment that is no longer Pending is left as it is.
    /// Only the calculation is caught (it marks the assessment Failed); a failure to load or save reaches the caller,
    /// and the row stays Pending until the next startup re-enqueue.
    /// </summary>
    public async Task<AssessmentProcessingResult> ProcessAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        var work = await repository.FindForProcessingAsync(assessmentId, cancellationToken);
        if (work is null)
        {
            return new AssessmentProcessingResult(AssessmentProcessingOutcome.NotFound, null, null);
        }

        var assessment = work.Assessment;
        if (assessment.Status != AssessmentStatus.Pending)
        {
            return new AssessmentProcessingResult(AssessmentProcessingOutcome.Skipped, assessment.ApplicationId, null);
        }

        // If the clock moved back since the row was saved (time sync), finish at the creation time instead of
        // throwing: the entity rejects a finish time before CreatedAt.
        var now = timeProvider.GetUtcNow();
        var finishedAt = now < assessment.CreatedAt ? assessment.CreatedAt : now;
        var result = TryAssess(work.Inputs);
        if (result is null)
        {
            assessment.Fail(finishedAt);
        }
        else
        {
            assessment.Complete(result, finishedAt);
        }

        await repository.UpdateAssessmentAsync(assessment, cancellationToken);
        return result is null
            ? new AssessmentProcessingResult(AssessmentProcessingOutcome.Failed, assessment.ApplicationId, null, null)
            : new AssessmentProcessingResult(AssessmentProcessingOutcome.Completed, assessment.ApplicationId, result.Decision, result.RulesVersion);
    }

    /// <summary>For the worker's startup re-enqueue: every Pending assessment, oldest first. Not owner-scoped.</summary>
    public Task<IReadOnlyList<Guid>> ListPendingAssessmentIdsAsync(CancellationToken cancellationToken) =>
        repository.ListPendingAssessmentIdsAsync(cancellationToken);

    // Stored inputs can break today's rules (a row written before a rules change); the calculator then throws.
    // The calculator is pure (no I/O, no cancellation), so any exception here is a calculation error, and its
    // message never contains an input value (InputGuard).
    private AffordabilityResult? TryAssess(AffordabilityInputs inputs)
    {
        try
        {
            return calculator.Assess(inputs);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
