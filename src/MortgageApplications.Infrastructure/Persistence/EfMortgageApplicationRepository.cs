using Microsoft.EntityFrameworkCore;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation. Reads are no-tracking and always filter on the owner; an assessment is found only
/// through an application the owner has (security S2), so another user's IDs behave like unknown IDs.
/// </summary>
internal sealed class EfMortgageApplicationRepository(MortgageApplicationsDbContext db) : IMortgageApplicationRepository
{
    public async Task AddAsync(MortgageApplication application, CancellationToken cancellationToken)
    {
        db.Applications.Add(application);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<MortgageApplication?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        db.Applications
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == id && a.OwnerId == ownerId, cancellationToken);

    public async Task AddAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken)
    {
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<AffordabilityAssessment?> FindAssessmentAsync(
        Guid applicationId, Guid assessmentId, string ownerId, CancellationToken cancellationToken) =>
        db.Assessments
            .AsNoTracking()
            .Where(s => s.Id == assessmentId
                && s.ApplicationId == applicationId
                && db.Applications.Any(a => a.Id == applicationId && a.OwnerId == ownerId))
            .SingleOrDefaultAsync(cancellationToken);
}
