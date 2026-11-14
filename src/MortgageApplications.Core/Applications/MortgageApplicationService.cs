using MortgageApplications.Core.Affordability;

namespace MortgageApplications.Core.Applications;

/// <summary>
/// The use cases: create, get, assess and get an assessment. The owner always comes from the caller
/// (the current user), never from request data; "not found" and "another owner's" both return <c>null</c>.
/// Times come from the injected <see cref="TimeProvider"/>, so the calculator stays pure.
/// </summary>
public sealed class MortgageApplicationService(
    IMortgageApplicationRepository repository,
    AffordabilityCalculator calculator,
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

    /// <summary>Assesses the stored inputs synchronously and saves the Completed assessment (Phase 6 makes it a background job).</summary>
    public async Task<AffordabilityAssessment?> AssessAsync(
        string ownerId, Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await repository.FindAsync(applicationId, ownerId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        // One reading of the clock for both times: a second reading could go backwards if the system clock is
        // adjusted, and Complete rejects a finish time before the creation time.
        var now = timeProvider.GetUtcNow();
        var assessment = AffordabilityAssessment.CreatePending(application.Id, now);
        assessment.Complete(calculator.Assess(application.ToInputs()), now);
        await repository.AddAssessmentAsync(assessment, cancellationToken);
        return assessment;
    }

    public Task<AffordabilityAssessment?> GetAssessmentAsync(
        string ownerId, Guid applicationId, Guid assessmentId, CancellationToken cancellationToken) =>
        repository.FindAssessmentAsync(applicationId, assessmentId, ownerId, cancellationToken);
}
