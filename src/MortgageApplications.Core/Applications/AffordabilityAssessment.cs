using MortgageApplications.Core.Affordability;

namespace MortgageApplications.Core.Applications;

public sealed class AffordabilityAssessment
{
    public Guid Id { get; }

    public Guid ApplicationId { get; }

    public AssessmentStatus Status { get; private set; }

    public AffordabilityResult? Result { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? AssessedAt { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    public static AffordabilityAssessment CreatePending(Guid applicationId, DateTimeOffset createdAt) =>
        throw new NotImplementedException();

    public void Complete(AffordabilityResult result, DateTimeOffset assessedAt) => throw new NotImplementedException();

    public void Fail(DateTimeOffset failedAt) => throw new NotImplementedException();
}
