using System.Buffers.Text;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MortgageApplications.Api.Identity;

/// <summary>
/// The <c>sub</c> claim rules: exactly one claim, a JSON string in the token (not a number, an object or an array),
/// not blank, at most 128 characters (the owner column size).
/// </summary>
internal static class SubjectClaim
{
    public const string Type = "sub";

    public const int MaxLength = 128;

    public static bool IsValid(string? subject) => !string.IsNullOrWhiteSpace(subject) && subject.Length <= MaxLength;

    public static bool IsValid(ClaimsPrincipal? principal, SecurityToken? token)
    {
        var subjects = principal?.FindAll(Type).ToList() ?? [];
        return subjects.Count == 1
            && subjects[0].ValueType == ClaimValueTypes.String
            && IsValid(subjects[0].Value)
            && IsJsonString(token);
    }

    // IdentityModel turns a numeric sub into a claim of type String, so the claim alone cannot tell "12345" from
    // 12345; the token's own payload can.
    private static bool IsJsonString(SecurityToken? token)
    {
        if (token is not JsonWebToken jwt)
        {
            return false;
        }

        using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(jwt.EncodedPayload));
        return payload.RootElement.TryGetProperty(Type, out var subject) && subject.ValueKind == JsonValueKind.String;
    }
}
