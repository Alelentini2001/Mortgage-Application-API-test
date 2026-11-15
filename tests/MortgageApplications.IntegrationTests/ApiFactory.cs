using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MortgageApplications.Api.Assessments;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// The real app on a named, shared-cache in-memory SQLite database per factory (P3-D7, P6-D3). The background
/// worker runs on another thread, and a SQLite connection must not be used by two threads at once, so each
/// <c>DbContext</c> opens its own connection to the same named database; one more connection, held by the factory,
/// keeps the database alive. Every host built from the factory (including <c>WithWebHostBuilder</c> variants for
/// another owner, A12) sees the same database. Migrations are applied once, before any host starts, so the worker's
/// startup query finds the tables. The environment is Development unless a test changes it.
/// <para>
/// The worker runs in the factory's default host only. Hosts from <see cref="WithWebHostBuilder(Action{IWebHostBuilder})"/>
/// run without it, so two workers never share a database by accident (Q2); a test opts in with
/// <see cref="WithWebHostBuilder(Action{IWebHostBuilder}, bool)"/>.
/// </para>
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = $"tests-{Guid.NewGuid():N}",
        Mode = SqliteOpenMode.Memory,
        Cache = SqliteCacheMode.Shared,
    }.ToString();

    private readonly SqliteConnection _keepAlive;
    private bool _keepAliveDisposed;

    public ApiFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
        using var db = new MortgageApplicationsDbContext(
            new DbContextOptionsBuilder<MortgageApplicationsDbContext>().UseSqlite(_keepAlive).Options);
        db.Database.Migrate();
    }

    /// <summary>Every log entry from every host of this factory.</summary>
    public CapturingLoggerProvider Logs { get; } = new();

    /// <summary>A host with extra configuration and without the worker.</summary>
    public new WebApplicationFactory<Program> WithWebHostBuilder(Action<IWebHostBuilder> configuration) =>
        WithWebHostBuilder(configuration, runWorker: false);

    /// <summary>A host with extra configuration; <paramref name="runWorker"/> keeps its background worker.</summary>
    public WebApplicationFactory<Program> WithWebHostBuilder(Action<IWebHostBuilder> configuration, bool runWorker) =>
        base.WithWebHostBuilder(builder =>
        {
            configuration(builder);
            if (!runWorker)
            {
                builder.ConfigureTestServices(services => services.Remove(services.Single(d =>
                    d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(AssessmentWorker))));
            }
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // .NET 10 pattern (Learn, integration tests): drop the app's options configuration, then re-add the context.
            services.RemoveAll<IDbContextOptionsConfiguration<MortgageApplicationsDbContext>>();
            services.AddDbContext<MortgageApplicationsDbContext>(options => options.UseSqlite(_connectionString));
            services.AddSingleton<ILoggerProvider>(Logs);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && !_keepAliveDisposed)
        {
            // The base class calls this method again from DisposeAsync; release the database only once.
            _keepAliveDisposed = true;
            // Pooled connections would otherwise keep this in-memory database alive after the factory is gone.
            SqliteConnection.ClearPool(_keepAlive);
            _keepAlive.Dispose();
        }
    }
}
