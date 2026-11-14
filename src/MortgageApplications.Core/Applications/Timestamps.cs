namespace MortgageApplications.Core.Applications;

/// <summary>
/// Entity timestamps are passed in by the caller (from TimeProvider). They must be UTC and not before
/// the Unix epoch: Guid.CreateVersion7 rejects earlier times with a message that contains the value.
/// </summary>
internal static class Timestamps
{
    public static void EnsureUtc(DateTimeOffset value, string paramName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Must be a UTC time (offset zero).", paramName);
        }

        if (value < DateTimeOffset.UnixEpoch)
        {
            throw new ArgumentException("Must not be before 1970-01-01 (Unix epoch).", paramName);
        }
    }
}
