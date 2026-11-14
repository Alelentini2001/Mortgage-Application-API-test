using MortgageApplications.Core.Affordability;

namespace MortgageApplications.Core.Applications;

/// <summary>
/// The use cases: create, get, assess and get an assessment. The owner always comes from the caller
/// (the current user), never from request data; "not found" and "another owner's" both return <c>null</c>.
/// </summary>
#pragma warning disable CS9113 // Red-phase stub only: removed with the implementation.
public sealed class MortgageApplicationService(
    IMortgageApplicationRepository repository,
    AffordabilityCalculator calculator,
    TimeProvider timeProvider)
{
    public Task<MortgageApplication> CreateAsync(
        string ownerId, AffordabilityInputs inputs, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<MortgageApplication?> GetAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    /// <summary>Assesses the stored inputs synchronously and saves the Completed assessment (Phase 6 makes it a background job).</summary>
    public Task<AffordabilityAssessment?> AssessAsync(
        string ownerId, Guid applicationId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<AffordabilityAssessment?> GetAssessmentAsync(
        string ownerId, Guid applicationId, Guid assessmentId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
