namespace MortgageApplications.Core.Affordability;

/// <summary>Decimal-only maths for the affordability rules (no double, no Math.Pow).</summary>
internal static class DecimalMath
{
    /// <summary>value^exponent by exponentiation by squaring, for a non-negative exponent.</summary>
    public static decimal Pow(decimal value, int exponent)
    {
        if (exponent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exponent), "Must not be negative.");
        }

        var result = 1m;
        var square = value;
        while (exponent > 0)
        {
            if ((exponent & 1) == 1)
            {
                result *= square;
            }

            exponent >>= 1;
            if (exponent > 0)
            {
                square *= square;
            }
        }

        return result;
    }

    /// <summary>
    /// Rounds once to 2 dp with MidpointRounding.AwayFromZero (the default is banker's rounding),
    /// then adds 0.00m so the result always has scale 2 (80 → 80.00).
    /// </summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero) + 0.00m;

    /// <summary>True when the value needs no more than the given number of decimal places (100.500m has 1).</summary>
    public static bool HasAtMostDecimals(decimal value, int decimals) => Math.Round(value, decimals) == value;
}
