using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using MortgageApplications.Api.Identity;
using MortgageApplications.Api.Observability;
using MortgageApplications.Api.Security;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.Api.Applications;

/// <summary>
/// The four application endpoints. IDs use the <c>guid</c> route constraint, so a malformed ID matches no route
/// (404). Unknown and other owners' IDs both give an empty 404 that the status-code middleware turns into the
/// same ProblemDetails, so nothing reveals whether an ID exists.
/// </summary>
public static partial class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/applications").WithTags("Applications");

        group.MapPost("", CreateAsync)
            .RequireRateLimiting(RateLimitingSetup.WritePolicyName)
            .WithSummary("Create a mortgage application")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        group.MapGet("/{id:guid}", GetAsync)
            .WithSummary("Get an application")
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/{id:guid}/assessments", AssessAsync)
            .RequireRateLimiting(RateLimitingSetup.WritePolicyName)
            .WithSummary("Start an affordability assessment (202 Accepted; poll the Location until Completed or Failed)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/{id:guid}/assessments/{assessmentId:guid}", GetAssessmentAsync)
            .WithSummary("Get an assessment (always 200; status is Pending, Completed or Failed; Declined is a Completed result)")
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

    // 202 + Location (P6-D4): the body is the Pending assessment; the client polls the Location until the status is
    // Completed or Failed. When the queue is full the request waits for room (P6-D2), bounded by its own token and
    // Assessments:EnqueueTimeoutSeconds (P8-D2 A, 503 when full past the timeout; the saved Pending row recovers at
    // startup). Once queued, the request's server span gets the assessment ID, so a trace view finds the worker's span
    // with the same tag (P5-D3 A), and the queued log carries the two IDs only. A 404 records neither.
    private static async Task<Results<Accepted<AssessmentResponse>, NotFound, StatusCodeHttpResult>> AssessAsync(
        Guid id,
        MortgageApplicationService service,
        ICurrentUser user,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        AffordabilityAssessment? assessment;
        try
        {
            assessment = await service.AssessAsync(user.OwnerId, id, cancellationToken);
        }
        catch (TimeoutException)
        {
            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (assessment is null)
        {
            return TypedResults.NotFound();
        }

        httpContext.Features.Get<IHttpActivityFeature>()?.Activity
            .SetTag(AssessmentTelemetry.AssessmentIdTag, assessment.Id.ToString());
        var logger = loggerFactory.CreateLogger(typeof(ApplicationEndpoints));
        LogAssessmentQueued(logger, assessment.Id, id);
        return TypedResults.Accepted(
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

    [LoggerMessage(EventId = 7, EventName = "AssessmentQueued", Level = LogLevel.Information,
        Message = "Assessment {AssessmentId} of application {ApplicationId} queued.")]
    private static partial void LogAssessmentQueued(ILogger logger, Guid assessmentId, Guid applicationId);
}
