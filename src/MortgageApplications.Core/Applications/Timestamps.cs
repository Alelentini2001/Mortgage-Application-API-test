namespace MortgageApplications.Core.Applications;

/// <summary>Entity timestamps are passed in by the caller (from TimeProvider) and must be UTC.</summary>
internal static class Timestamps
{
    public static void EnsureUtc(DateTimeOffset value, string paramName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Must be a UTC time (offset zero).", paramName);
        }
    }
}
