namespace MortgageApplications.Core.Applications;

/// <summary>
/// Persistence for applications and their assessments. Every read for a caller takes the owner, so a lookup can
/// never return another user's data: callers get <c>null</c> for "not found" and "not yours" alike.
/// The processing methods are not owner-scoped: only the background worker uses them, with IDs from its own queue.
/// The add and update methods save straight away.
/// </summary>
public interface IMortgageApplicationRepository
{
    Task AddAsync(MortgageApplication application, CancellationToken cancellationToken);

    Task<MortgageApplication?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    Task AddAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken);

    /// <summary>The assessment, only when it belongs to that application and the application belongs to the owner.</summary>
    Task<AffordabilityAssessment?> FindAssessmentAsync(
        Guid applicationId, Guid assessmentId, string ownerId, CancellationToken cancellationToken);

    /// <summary>Worker only, not owner-scoped: the assessment with its application's stored inputs, or null.</summary>
    Task<AssessmentWorkItem?> FindForProcessingAsync(Guid assessmentId, CancellationToken cancellationToken);

    /// <summary>Saves a change to an assessment returned by <see cref="FindForProcessingAsync"/>.</summary>
    Task UpdateAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken);

    /// <summary>Worker only, not owner-scoped: the IDs of every Pending assessment, oldest first.</summary>
    Task<IReadOnlyList<Guid>> ListPendingAssessmentIdsAsync(CancellationToken cancellationToken);
}
