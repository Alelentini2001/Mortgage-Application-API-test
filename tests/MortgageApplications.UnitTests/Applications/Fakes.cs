using MortgageApplications.Core.Applications;

namespace MortgageApplications.UnitTests.Applications;

/// <summary>In-memory repository with the same owner rules as the EF one (every read filters on the owner).</summary>
internal sealed class FakeRepository : IMortgageApplicationRepository
{
    public List<MortgageApplication> Applications { get; } = [];

    public List<AffordabilityAssessment> Assessments { get; } = [];

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
}

/// <summary>A clock fixed at one UTC instant (A11: no FakeTimeProvider package).</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
