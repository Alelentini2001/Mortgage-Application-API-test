namespace MortgageApplications.Api.Identity;

/// <summary>
/// The caller's identity. The owner of every application comes only from here, never from a request body. It is the
/// <c>sub</c> claim of the validated JWT bearer token (<see cref="ClaimsCurrentUser"/>).
/// </summary>
public interface ICurrentUser
{
    string OwnerId { get; }
}
