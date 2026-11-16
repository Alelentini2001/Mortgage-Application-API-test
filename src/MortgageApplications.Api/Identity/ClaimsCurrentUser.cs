namespace MortgageApplications.Api.Identity;

/// <summary>
/// The owner is the authenticated caller's <c>sub</c> claim. The fallback policy means every endpoint that uses this
/// has an authenticated caller, and token validation already rejected a missing, blank or over-long subject
/// (<see cref="SubjectClaim"/>), so the exception is a programming error, never a client error.
/// </summary>
internal sealed class ClaimsCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string OwnerId
    {
        get
        {
            var subject = httpContextAccessor.HttpContext?.User.FindFirst(SubjectClaim.Type)?.Value;
            return SubjectClaim.IsValid(subject)
                ? subject!
                : throw new InvalidOperationException("The request has no authenticated subject.");
        }
    }
}
