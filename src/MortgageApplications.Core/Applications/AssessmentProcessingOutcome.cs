namespace MortgageApplications.Core.Applications;

/// <summary>What <see cref="MortgageApplicationService.ProcessAssessmentAsync"/> did with one queued ID.</summary>
public enum AssessmentProcessingOutcome
{
    /// <summary>No assessment (or no application) with that ID; nothing changed.</summary>
    NotFound,

    /// <summary>The assessment had already finished (queued twice, or finished before a restart); nothing changed.</summary>
    Skipped,

    Completed,

    /// <summary>The calculation threw, so the assessment was saved as Failed.</summary>
    Failed,
}
