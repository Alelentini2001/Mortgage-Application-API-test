using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;
using MortgageApplications.Infrastructure;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Phase 9 database verification (P9-D2 A, <c>PRODUCTION-LAYERS.md</c> "Database"): SQLite <c>EXPLAIN QUERY PLAN</c>
/// proves the repository's point and foreign-key lookups use primary-key and foreign-key indexes (<c>SEARCH</c>, never
/// a full-table <c>SCAN</c>), and <see cref="DatabaseMigration.MigrateDatabaseAsync"/> enables
/// <c>PRAGMA journal_mode=WAL</c> on file-backed SQLite databases while keeping in-memory test databases at
/// <c>memory</c>.
/// </summary>
public sealed class DatabaseQueryPlanTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly AffordabilityInputs _e1 = new(85_000m, 900m, 60_000m, 300_000m, 30);

    // P9-T3: FindAsync, FindAssessmentAsync and FindForProcessingAsync all use index/PK SEARCH plans and never SCAN.
    [Fact]
    public async Task Repository_lookup_queries_use_index_search_and_never_full_table_scan()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IMortgageApplicationRepository>();
        var now = DateTimeOffset.UtcNow;
        var application = MortgageApplication.Create("plan-owner", _e1, now);
        var assessment = AffordabilityAssessment.CreatePending(application.Id, now);
        await repository.AddAsync(application, Http.Ct);
        await repository.AddAssessmentAsync(assessment, Http.Ct);

        var findApplicationQuery = db.Applications
            .AsNoTracking()
            .Where(a => a.Id == application.Id && a.OwnerId == "plan-owner");
        var findAssessmentQuery = db.Assessments
            .AsNoTracking()
            .Where(s => s.Id == assessment.Id
                && s.ApplicationId == application.Id
                && db.Applications.Any(a => a.Id == application.Id && a.OwnerId == "plan-owner"));
        var findForProcessingQuery = db.Assessments
            .Where(s => s.Id == assessment.Id)
            .Join(db.Applications, s => s.ApplicationId, a => a.Id, (s, a) => new { s.Id, a.AnnualIncome });
        var findByOwnerQuery = db.Applications
            .AsNoTracking()
            .Where(a => a.OwnerId == "plan-owner");

        foreach (var query in new IQueryable[] { findApplicationQuery, findAssessmentQuery, findForProcessingQuery, findByOwnerQuery })
        {
            var details = await ExplainQueryPlanAsync(db, query);
            Assert.NotEmpty(details);
            Assert.All(details, detail =>
            {
                Assert.Contains("SEARCH", detail, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("SCAN", detail, StringComparison.OrdinalIgnoreCase);
            });
        }

        Assert.Contains(
            await ExplainQueryPlanAsync(db, findByOwnerQuery),
            d => d.Contains("IX_MortgageApplications_OwnerId", StringComparison.Ordinal));
    }

    // P9-T4: MigrateDatabaseAsync sets PRAGMA journal_mode=WAL on a file-backed SQLite database while keeping in-memory
    // databases at "memory".
    [Fact]
    public async Task MigrateDatabaseAsync_enables_wal_mode_on_file_database()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"mortgage-wal-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection()
                .AddInfrastructure($"Data Source={tempFile}")
                .BuildServiceProvider();
            await using (services)
            {
                await services.MigrateDatabaseAsync(Http.Ct);
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
                var mode = await QueryScalarStringAsync(db, "PRAGMA journal_mode;");
                Assert.Equal("wal", mode, ignoreCase: true);
            }
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                File.Delete(tempFile + suffix);
            }
        }

        await using var inMemoryScope = factory.Services.CreateAsyncScope();
        var inMemoryDb = inMemoryScope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        Assert.Equal("memory", await QueryScalarStringAsync(inMemoryDb, "PRAGMA journal_mode;"), ignoreCase: true);
    }

    private static async Task<List<string>> ExplainQueryPlanAsync(MortgageApplicationsDbContext db, IQueryable query)
    {
        await db.Database.OpenConnectionAsync(Http.Ct);
        await using var command = query.CreateDbCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN {command.CommandText}";
        var details = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(Http.Ct);
        while (await reader.ReadAsync(Http.Ct))
        {
            details.Add(reader.GetString(3));
        }

        return details;
    }

    private static async Task<string> QueryScalarStringAsync(MortgageApplicationsDbContext db, string sql)
    {
        await db.Database.OpenConnectionAsync(Http.Ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync(Http.Ct))!;
    }
}
