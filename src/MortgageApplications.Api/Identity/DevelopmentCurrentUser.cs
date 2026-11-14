namespace MortgageApplications.Api.Identity;

/// <summary>
/// One fixed owner until Phase 7 replaces this with the JWT <c>sub</c> claim. Accepted exception: builds before
/// Phase 7 are not deployed or submitted (docs/layers/PRODUCTION-LAYERS.md, "Auth &amp; permissions").
/// </summary>
internal sealed class DevelopmentCurrentUser : ICurrentUser
{
    public string OwnerId => "dev-owner-0001";
}
