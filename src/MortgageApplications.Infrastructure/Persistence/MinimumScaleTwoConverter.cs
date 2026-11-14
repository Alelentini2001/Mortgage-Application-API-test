using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MortgageApplications.Infrastructure.Persistence;

/// <summary>
/// SQLite stores <c>decimal</c> as TEXT and can drop trailing zeros, so 240000.00 may come back as 240000.0.
/// Adding 0.00m on read restores at least 2 decimal places without changing the value (rules section 4:
/// every output has scale 2). Values with more places, like a 4 dp rate, keep them.
/// </summary>
internal sealed class MinimumScaleTwoConverter() : ValueConverter<decimal, decimal>(v => v, v => v + 0.00m);
