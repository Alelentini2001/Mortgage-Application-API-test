using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace MortgageApplications.Api.Identity;

/// <summary>Reads <c>Jwt:SigningPublicKey</c>: a "PUBLIC KEY" PEM block on the NIST P-256 curve, nothing else.</summary>
internal static class SigningPublicKey
{
    private const string _p256Oid = "1.2.840.10045.3.1.7";

    public static bool TryRead(string pem, [NotNullWhen(true)] out ECDsa? key)
    {
        key = null;
        if (!PemEncoding.TryFind(pem, out var fields) || pem[fields.Label] is not "PUBLIC KEY")
        {
            return false;
        }

        var candidate = ECDsa.Create();
        try
        {
            candidate.ImportSubjectPublicKeyInfo(Convert.FromBase64String(pem[fields.Base64Data].ToString()), out _);
            if (candidate.ExportParameters(includePrivateParameters: false).Curve.Oid.Value != _p256Oid)
            {
                candidate.Dispose();
                return false;
            }
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            candidate.Dispose();
            return false;
        }

        key = candidate;
        return true;
    }
}
