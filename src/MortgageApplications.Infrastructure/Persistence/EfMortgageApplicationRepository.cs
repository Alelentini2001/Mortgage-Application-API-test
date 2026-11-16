using Microsoft.EntityFrameworkCore;
using MortgageApplications.Core.Affordability;
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

    // Worker only (not owner-scoped). Tracks only the assessment (projected scalar inputs avoid materializing or
    // tracking the parent application, PR #8 /code-review 3), so the change the worker makes is saved by
    // UpdateAssessmentAsync in this context.
    public async Task<AssessmentWorkItem?> FindForProcessingAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        var row = await db.Assessments
            .Where(s => s.Id == assessmentId)
            .Join(
                db.Applications,
                s => s.ApplicationId,
                a => a.Id,
                (s, a) => new
                {
                    Assessment = s,
                    a.AnnualIncome,
                    a.MonthlyExpenses,
                    a.DepositAmount,
                    a.PropertyValue,
                    a.TermYears,
                })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new AssessmentWorkItem(
                row.Assessment,
                new AffordabilityInputs(
                    row.AnnualIncome,
                    row.MonthlyExpenses,
                    row.DepositAmount,
                    row.PropertyValue,
                    row.TermYears));
    }

    public async Task UpdateAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken)
    {
        // When loaded by FindForProcessingAsync in this context, EF already tracks which properties changed;
        // calling Update is needed only for a detached instance (PR #8 /code-review 3, P9-D1 A).
        if (db.Entry(assessment).State == EntityState.Detached)
        {
            db.Assessments.Update(assessment);
        }

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
