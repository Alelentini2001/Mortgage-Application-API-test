using MortgageApplications.Core.Affordability;

namespace MortgageApplications.Core.Applications;

/// <summary>
/// What <see cref="MortgageApplicationService.ProcessAssessmentAsync"/> did with one queued ID, plus the fields the
/// worker may report (rules section 6 allowlist): the application ID when the assessment was found, and the decision
/// when it was Completed. Never an input or output value.
/// </summary>
public sealed record AssessmentProcessingResult(
    AssessmentProcessingOutcome Outcome,
    Guid? ApplicationId,
    Decision? Decision);
