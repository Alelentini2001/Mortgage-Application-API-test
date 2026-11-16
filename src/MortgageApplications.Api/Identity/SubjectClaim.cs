using System.Security.Claims;

namespace MortgageApplications.Api.Identity;

/// <summary>The <c>sub</c> claim rules: exactly one, not blank, at most 128 characters (the owner column size).</summary>
internal static class SubjectClaim
{
    public const string Type = "sub";

    public const int MaxLength = 128;

    public static bool IsValid(string? subject) => !string.IsNullOrWhiteSpace(subject) && subject.Length <= MaxLength;

    public static bool IsValid(ClaimsPrincipal? principal)
    {
        var subjects = principal?.FindAll(Type).ToList() ?? [];
        return subjects.Count == 1 && IsValid(subjects[0].Value);
    }
}
