using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using MortgageApplications.Api.Security;

namespace MortgageApplications.Api.Identity;

/// <summary>
/// <c>POST /dev/token</c> (P7-D3 A): a token for a subject the caller picks, for local runs, demos and CI only. No
/// password and no user store (Learn: "Limit creating access tokens to test scenarios"). Program maps it only in
/// Development with <c>DevAuth:Enabled=true</c>; anywhere else the route does not exist.
/// </summary>
internal static class DevTokenEndpoint
{
    public static IEndpointRouteBuilder MapDevTokenEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/dev/token", Issue)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingSetup.WritePolicyName)
            .WithTags("Development")
            .WithSummary("Development only: issue a token for a subject")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        return endpoints;
    }

    private static Ok<DevTokenResponse> Issue(DevTokenRequest request, DevTokenIssuer issuer) =>
        TypedResults.Ok(issuer.Issue(request.Subject!));
}

/// <summary>The subject must match the whole pattern (A8: <c>\z</c>, so a trailing newline is rejected).</summary>
public sealed record DevTokenRequest
{
    /// <summary>1 to 64 lowercase letters, digits or hyphens.</summary>
    [Required(ErrorMessage = "Required.")]
    [RegularExpression(@"^[a-z0-9-]{1,64}\z", ErrorMessage = "Must be 1 to 64 lowercase letters, digits or hyphens.")]
    public string? Subject { get; init; }

    /// <summary>The subject is an owner ID and must never reach a log (rules section 6).</summary>
    public override string ToString() => $"{nameof(DevTokenRequest)} {{ redacted }}";
}

/// <summary>A signed token and the time it expires (its <c>exp</c> claim).</summary>
public sealed record DevTokenResponse(string Token, DateTimeOffset ExpiresAt)
{
    /// <summary>Leaves out the token so formatting the record never logs a bearer token (rules section 6).</summary>
    public override string ToString() => $"{nameof(DevTokenResponse)} {{ ExpiresAt = {ExpiresAt:O} }}";
}
