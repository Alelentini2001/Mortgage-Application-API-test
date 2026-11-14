using Microsoft.AspNetCore.Http.HttpResults;
using MortgageApplications.Api.Identity;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.Api.Applications;

/// <summary>
/// The four application endpoints. IDs use the <c>guid</c> route constraint, so a malformed ID matches no route
/// (404). Unknown and other owners' IDs both give an empty 404 that the status-code middleware turns into the
/// same ProblemDetails, so nothing reveals whether an ID exists.
/// </summary>
public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/applications").WithTags("Applications");

        group.MapPost("", CreateAsync)
            .WithSummary("Create a mortgage application")
            .ProducesValidationProblem();
        group.MapGet("/{id:guid}", GetAsync)
            .WithSummary("Get an application")
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/{id:guid}/assessments", AssessAsync)
            .WithSummary("Assess affordability (synchronous; Declined is a 201 result, not an error)")
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/{id:guid}/assessments/{assessmentId:guid}", GetAssessmentAsync)
            .WithSummary("Get an assessment")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Created<ApplicationResponse>> CreateAsync(
        CreateApplicationRequest request,
        MortgageApplicationService service,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var application = await service.CreateAsync(user.OwnerId, request.ToInputs(), cancellationToken);
        return TypedResults.Created($"/api/applications/{application.Id}", ApplicationResponse.From(application));
    }

    private static async Task<Results<Ok<ApplicationResponse>, NotFound>> GetAsync(
        Guid id,
        MortgageApplicationService service,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var application = await service.GetAsync(user.OwnerId, id, cancellationToken);
        return application is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ApplicationResponse.From(application));
    }

    private static async Task<Results<Created<AssessmentResponse>, NotFound>> AssessAsync(
        Guid id,
        MortgageApplicationService service,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var assessment = await service.AssessAsync(user.OwnerId, id, cancellationToken);
        return assessment is null
            ? TypedResults.NotFound()
            : TypedResults.Created(
                $"/api/applications/{id}/assessments/{assessment.Id}",
                AssessmentResponse.From(assessment));
    }

    private static async Task<Results<Ok<AssessmentResponse>, NotFound>> GetAssessmentAsync(
        Guid id,
        Guid assessmentId,
        MortgageApplicationService service,
        ICurrentUser user,
        CancellationToken cancellationToken)
    {
        var assessment = await service.GetAssessmentAsync(user.OwnerId, id, assessmentId, cancellationToken);
        return assessment is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(AssessmentResponse.From(assessment));
    }
}
