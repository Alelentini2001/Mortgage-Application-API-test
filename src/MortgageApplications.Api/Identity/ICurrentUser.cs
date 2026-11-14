namespace MortgageApplications.Api.Identity;

/// <summary>
/// The caller's identity. The owner of every application comes only from here, never from a request body.
/// Until Phase 7 (JWT) it is one fixed development owner.
/// </summary>
public interface ICurrentUser
{
    string OwnerId { get; }
}
