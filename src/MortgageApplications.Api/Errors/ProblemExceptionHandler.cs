using Microsoft.AspNetCore.Diagnostics;

namespace MortgageApplications.Api.Errors;

/// <summary>
/// Turns any exception into a ProblemDetails with the trace ID and no exception text, in every environment
/// (security S5, A4). A <see cref="BadHttpRequestException"/> (malformed JSON, wrong content type) keeps its own
/// 4xx status: in .NET 10 it would otherwise become a 500 in Development, where binding failures throw.
/// </summary>
internal sealed class ProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception is BadHttpRequestException badRequest
            ? badRequest.StatusCode
            : StatusCodes.Status500InternalServerError;
        httpContext.Response.StatusCode = status;

        // Title, type and traceId are filled in by the default writer from the status code. No Detail and no
        // Exception on the context: neither may reach the client.
        return problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = { Status = status },
        });
    }
}
