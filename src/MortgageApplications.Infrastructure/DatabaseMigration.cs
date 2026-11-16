using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.Infrastructure;

public static class DatabaseMigration
{
    /// <summary>
    /// Applies pending migrations. The API calls this at startup in Development only; in production migrations
    /// run as a separate deployment step (docs/layers/PRODUCTION-LAYERS.md, "Hosting / deploy"; ADR 0002).
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
    }
}
