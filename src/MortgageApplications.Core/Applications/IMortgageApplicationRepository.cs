namespace MortgageApplications.Core.Applications;

/// <summary>
/// Persistence for applications and their assessments. Every read takes the owner, so a lookup can never
/// return another user's data: callers get <c>null</c> for "not found" and "not yours" alike.
/// The add methods save straight away.
/// </summary>
public interface IMortgageApplicationRepository
{
    Task AddAsync(MortgageApplication application, CancellationToken cancellationToken);

    Task<MortgageApplication?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken);

    Task AddAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken);

    /// <summary>The assessment, only when it belongs to that application and the application belongs to the owner.</summary>
    Task<AffordabilityAssessment?> FindAssessmentAsync(
        Guid applicationId, Guid assessmentId, string ownerId, CancellationToken cancellationToken);
}
