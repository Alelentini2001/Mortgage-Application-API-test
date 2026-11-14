using MortgageApplications.Core.Affordability;

namespace MortgageApplications.Core.Applications;

/// <summary>
/// One affordability assessment of an application. Starts Pending and moves once, to Completed (with the
/// result, which carries the rules version, rate and limits used) or to Failed. Any second change throws.
/// </summary>
public sealed class AffordabilityAssessment
{
    private AffordabilityAssessment(Guid id, Guid applicationId, DateTimeOffset createdAt)
    {
        Id = id;
        ApplicationId = applicationId;
        CreatedAt = createdAt;
        Status = AssessmentStatus.Pending;
    }

    public Guid Id { get; }

    public Guid ApplicationId { get; }

    public AssessmentStatus Status { get; private set; }

    /// <summary>Set only when Completed.</summary>
    public AffordabilityResult? Result { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Set only when Completed.</summary>
    public DateTimeOffset? AssessedAt { get; private set; }

    /// <summary>Set only when Failed.</summary>
    public DateTimeOffset? FailedAt { get; private set; }

    public static AffordabilityAssessment CreatePending(Guid applicationId, DateTimeOffset createdAt)
    {
        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException("Must not be empty.", nameof(applicationId));
        }

        Timestamps.EnsureUtc(createdAt, nameof(createdAt));
        return new AffordabilityAssessment(Guid.CreateVersion7(createdAt), applicationId, createdAt);
    }

    /// <exception cref="InvalidOperationException">The assessment is no longer Pending.</exception>
    public void Complete(AffordabilityResult result, DateTimeOffset assessedAt)
    {
        ArgumentNullException.ThrowIfNull(result);
        EnsureFinishTime(assessedAt, nameof(assessedAt));
        EnsurePending();

        Result = result;
        AssessedAt = assessedAt;
        Status = AssessmentStatus.Completed;
    }

    /// <exception cref="InvalidOperationException">The assessment is no longer Pending.</exception>
    public void Fail(DateTimeOffset failedAt)
    {
        EnsureFinishTime(failedAt, nameof(failedAt));
        EnsurePending();

        FailedAt = failedAt;
        Status = AssessmentStatus.Failed;
    }

    private void EnsureFinishTime(DateTimeOffset value, string paramName)
    {
        Timestamps.EnsureUtc(value, paramName);
        if (value < CreatedAt)
        {
            throw new ArgumentException("Must not be before the assessment was created.", paramName);
        }
    }

    private void EnsurePending()
    {
        if (Status != AssessmentStatus.Pending)
        {
            throw new InvalidOperationException("The assessment has already finished.");
        }
    }
}
