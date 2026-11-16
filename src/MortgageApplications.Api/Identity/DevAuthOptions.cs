namespace MortgageApplications.Api.Identity;

/// <summary>
/// The <c>DevAuth</c> configuration section. <see cref="Enabled"/> maps <c>POST /dev/token</c> in Development only;
/// startup fails if it is true in any other environment (<see cref="DevAuthOptionsValidator"/>).
/// </summary>
public sealed class DevAuthOptions
{
    public const string SectionName = "DevAuth";

    public bool Enabled { get; set; }
}
