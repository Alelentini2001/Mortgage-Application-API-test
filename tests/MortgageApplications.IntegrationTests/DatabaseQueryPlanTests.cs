using System.Data.Common;
using Microsoft.Data.Sqlite;
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

    // P9-T3 (gate R1): captures the actual SQL commands executed by EfMortgageApplicationRepository (FindAsync,
    // FindAssessmentAsync, FindForProcessingAsync) plus the OwnerId and ApplicationId foreign-key index lookups, and
    // proves every plan uses index/PK SEARCH and never a full-table SCAN.
    [Fact]
    public async Task Repository_lookup_queries_use_index_search_and_never_full_table_scan()
    {
        await using var seedScope = factory.Services.CreateAsyncScope();
        var seedRepo = seedScope.ServiceProvider.GetRequiredService<IMortgageApplicationRepository>();
        var now = DateTimeOffset.UtcNow;
        var application = MortgageApplication.Create("plan-owner", _e1, now);
        var assessment = AffordabilityAssessment.CreatePending(application.Id, now);
        await seedRepo.AddAsync(application, Http.Ct);
        await seedRepo.AddAssessmentAsync(assessment, Http.Ct);

        var interceptor = new RecordingCommandInterceptor();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<MortgageApplicationsDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<MortgageApplicationsDbContext>((sp, options) =>
            {
                using var baseScope = factory.Services.CreateScope();
                var connString = baseScope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>()
                    .Database.GetConnectionString();
                options.UseSqlite(connString).AddInterceptors(interceptor);
            });
        }));

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IMortgageApplicationRepository>();
        interceptor.Commands.Clear();

        Assert.NotNull(await repository.FindAsync(application.Id, "plan-owner", Http.Ct));
        Assert.NotNull(await repository.FindAssessmentAsync(application.Id, assessment.Id, "plan-owner", Http.Ct));
        Assert.NotNull(await repository.FindForProcessingAsync(assessment.Id, Http.Ct));
        _ = await db.Applications.AsNoTracking().Where(a => a.OwnerId == "plan-owner").ToListAsync(Http.Ct);
        _ = await db.Assessments.AsNoTracking().Where(s => s.ApplicationId == application.Id).ToListAsync(Http.Ct);

        Assert.Equal(5, interceptor.Commands.Count);
        var allPlans = new List<string>();
        foreach (var captured in interceptor.Commands)
        {
            var details = await ExplainCapturedCommandAsync(db, captured);
            Assert.NotEmpty(details);
            Assert.All(details, detail =>
            {
                Assert.Contains("SEARCH", detail, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("SCAN", detail, StringComparison.OrdinalIgnoreCase);
            });
            allPlans.AddRange(details);
        }

        Assert.Contains(allPlans, d => d.Contains("IX_MortgageApplications_OwnerId", StringComparison.Ordinal));
        Assert.Contains(allPlans, d => d.Contains("IX_AffordabilityAssessments_ApplicationId", StringComparison.Ordinal));
    }

    // P9-T4 (gate R3): MigrateDatabaseAsync explicitly sets PRAGMA journal_mode=WAL on a file-backed SQLite database
    // (even when the file already exists in DELETE journal mode) while keeping in-memory databases at "memory".
    [Fact]
    public async Task MigrateDatabaseAsync_enables_wal_mode_on_file_database()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"mortgage-wal-{Guid.NewGuid():N}.db");
        try
        {
            await using (var pre = new SqliteConnection($"Data Source={tempFile}"))
            {
                await pre.OpenAsync(Http.Ct);
                await using var cmd = pre.CreateCommand();
                cmd.CommandText = "PRAGMA journal_mode=DELETE; CREATE TABLE __probe (id INT);";
                await cmd.ExecuteNonQueryAsync(Http.Ct);
            }

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

    private static async Task<List<string>> ExplainCapturedCommandAsync(
        MortgageApplicationsDbContext db, CapturedCommand captured)
    {
        await db.Database.OpenConnectionAsync(Http.Ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN {captured.CommandText}";
        foreach (var (name, value) in captured.Parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

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

    private sealed record CapturedCommand(string CommandText, IReadOnlyList<(string Name, object? Value)> Parameters);

    private sealed class RecordingCommandInterceptor : DbCommandInterceptor
    {
        public List<CapturedCommand> Commands { get; } = [];

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Capture(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Capture(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Capture(DbCommand command)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                var parameters = command.Parameters.Cast<DbParameter>()
                    .Select(p => (p.ParameterName, p.Value))
                    .ToList();
                Commands.Add(new CapturedCommand(command.CommandText, parameters));
            }
        }
    }
}
