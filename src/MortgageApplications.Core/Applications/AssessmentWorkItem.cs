using MortgageApplications.Core.Affordability;

namespace MortgageApplications.Core.Applications;

/// <summary>
/// An assessment to process and the inputs of its application. The inputs are read as stored, without the
/// creation checks, so a row written under older rules still reaches the calculator, which rejects it (Failed).
/// </summary>
public sealed record AssessmentWorkItem(AffordabilityAssessment Assessment, AffordabilityInputs Inputs);
