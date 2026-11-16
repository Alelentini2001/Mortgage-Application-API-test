using System.Net.Http.Headers;
using System.Security.Cryptography;
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
/// <para>
/// Authentication (Phase 7, P7-D4 A): the factory owns an ECDSA P-256 key and configures its public key as
/// <c>Jwt:SigningPublicKey</c> with <c>UseSetting</c>, for every host it builds. Every client it (or a host built
/// from it) creates sends a token for <see cref="DefaultOwner"/>; <see cref="AuthHttp.WithToken"/> switches a client to
/// another subject and <see cref="CreateAnonymousClient"/> sends no token.
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
    private readonly ECDsa _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private bool _keepAliveDisposed;

    /// <summary>The subject (owner) of the token every client sends by default.</summary>
    public const string DefaultOwner = "test-owner-0001";

    /// <summary>The issuer and audience in <c>appsettings.json</c>; tokens must carry both.</summary>
    public const string Issuer = "mortgage-applications";

    public const string Audience = "mortgage-applications-api";

    /// <summary>The factory's public key (SubjectPublicKeyInfo PEM), which the app trusts.</summary>
    public string PublicKeyPem => _signingKey.ExportSubjectPublicKeyInfoPem();

    /// <summary>A valid 15-minute token for a subject, signed with the factory's key.</summary>
    public string TokenFor(string? subject) => Sign(TokenSpec.Valid(subject));

    /// <summary>A token with the given claims and times, signed with the factory's key.</summary>
    public string Sign(TokenSpec spec) => TestTokens.Sign(spec, _signingKey);

    /// <summary>The factory's private key, for tests that build raw tokens.</summary>
    internal ECDsa SigningKey => _signingKey;

    /// <summary>A client of the default host that sends no Authorization header.</summary>
    public HttpClient CreateAnonymousClient() => CreateClient().WithoutToken();

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
        // Program reads configuration before Build, so the key goes in as a host setting (as in OtlpExportTests).
        builder.UseSetting("Jwt:SigningPublicKey", PublicKeyPem);
        builder.ConfigureServices(services =>
        {
            // .NET 10 pattern (Learn, integration tests): drop the app's options configuration, then re-add the context.
            services.RemoveAll<IDbContextOptionsConfiguration<MortgageApplicationsDbContext>>();
            services.AddDbContext<MortgageApplicationsDbContext>(options => options.UseSqlite(_connectionString));
            services.AddSingleton<ILoggerProvider>(Logs);
        });
    }

    /// <summary>
    /// Runs for every client of this factory and of every host built from it (the hosts reuse this method), after
    /// any handlers passed to <c>CreateDefaultClient</c> are in place, so the token composes with them. A request
    /// that sets its own Authorization header keeps it.
    /// </summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(DefaultOwner));
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
            _signingKey.Dispose();
        }
    }
}
