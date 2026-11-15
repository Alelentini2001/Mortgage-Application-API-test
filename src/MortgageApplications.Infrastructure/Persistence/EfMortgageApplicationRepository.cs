using Microsoft.EntityFrameworkCore;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation. Caller reads are no-tracking and always filter on the owner; an assessment is found only
/// through an application the owner has (security S2), so another user's IDs behave like unknown IDs. The worker's
/// methods are not owner-scoped (the IDs come from the worker's own queue).
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

    // Worker only (not owner-scoped). Tracked, so the change the worker makes is saved by UpdateAssessmentAsync in
    // this context. The inputs are read as stored: the entity constructor EF uses does not re-check them.
    public async Task<AssessmentWorkItem?> FindForProcessingAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        var row = await db.Assessments
            .Where(s => s.Id == assessmentId)
            .Join(db.Applications, s => s.ApplicationId, a => a.Id, (s, a) => new { Assessment = s, Application = a })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new AssessmentWorkItem(row.Assessment, row.Application.ToInputs());
    }

    public async Task UpdateAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken)
    {
        // Marks the assessment Modified, whether this context already tracks it (loaded above) or not.
        db.Assessments.Update(assessment);
        await db.SaveChangesAsync(cancellationToken);
    }

    // Worker only (not owner-scoped). No index on Status: this runs once per startup, so a scan is acceptable.
    // IDs are version 7 (time-ordered), so ordering by ID is oldest first.
    public async Task<IReadOnlyList<Guid>> ListPendingAssessmentIdsAsync(CancellationToken cancellationToken) =>
        await db.Assessments
            .AsNoTracking()
            .Where(s => s.Status == AssessmentStatus.Pending)
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
}
