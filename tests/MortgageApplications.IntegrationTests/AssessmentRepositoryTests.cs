using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// I-R1 to I-R3: the worker's repository methods on real SQLite. The host runs without the worker, so nothing
/// else changes the rows. Each step uses a new scope (a new DbContext), so reads come from the database.
/// </summary>
public sealed class AssessmentRepositoryTests(ApiFactory factory) : IClassFixture<ApiFactory>, IDisposable
{
    private const string _owner = "repository-owner";
    private static readonly AffordabilityInputs _e1 = new(85_000m, 900m, 60_000m, 300_000m, 30);

    private readonly WebApplicationFactory<Program> _host = factory.WithWebHostBuilder(_ => { });

    public void Dispose() => _host.Dispose();

    // I-R1: the query maps the string-stored status, so only Pending IDs come back.
    [Fact]
    public async Task ListPendingAssessmentIdsAsync_returns_only_pending_ids()
    {
        var pending = await SeedPendingAsync();
        var completed = await SeedPendingAsync();
        await FinishAsync(completed.Id, a => a.Complete(Calculator().Assess(_e1), Now()));
        var failed = await SeedPendingAsync();
        await FinishAsync(failed.Id, a => a.Fail(Now()));

        var ids = await WithRepositoryAsync(r => r.ListPendingAssessmentIdsAsync(Http.Ct));

        Assert.Contains(pending.Id, ids);
        Assert.DoesNotContain(completed.Id, ids);
        Assert.DoesNotContain(failed.Id, ids);
    }

    // I-R2: Complete then UpdateAssessmentAsync is saved; a fresh context reads the same status, time and result.
    [Fact]
    public async Task UpdateAssessmentAsync_completed_survives_a_reload()
    {
        var seeded = await SeedPendingAsync();
        var assessedAt = Now();
        var expected = Calculator().Assess(_e1);

        await FinishAsync(seeded.Id, a => a.Complete(expected, assessedAt));

        var reloaded = (await WithRepositoryAsync(r => r.FindAssessmentAsync(seeded.ApplicationId, seeded.Id, _owner, Http.Ct)))!;
        Assert.Equal(AssessmentStatus.Completed, reloaded.Status);
        Assert.Equal(assessedAt, reloaded.AssessedAt);
        Assert.Null(reloaded.FailedAt);
        var result = reloaded.Result!;
        Assert.Equal(expected with { Checks = result.Checks }, result);
        Assert.Equal(expected.Checks, result.Checks);
    }

    // I-R3
    [Fact]
    public async Task UpdateAssessmentAsync_failed_survives_a_reload()
    {
        var seeded = await SeedPendingAsync();
        var failedAt = Now();

        await FinishAsync(seeded.Id, a => a.Fail(failedAt));

        var reloaded = (await WithRepositoryAsync(r => r.FindAssessmentAsync(seeded.ApplicationId, seeded.Id, _owner, Http.Ct)))!;
        Assert.Equal(AssessmentStatus.Failed, reloaded.Status);
        Assert.Equal(failedAt, reloaded.FailedAt);
        Assert.Null(reloaded.AssessedAt);
        Assert.Null(reloaded.Result);
    }

    // P9-T2 (PR #8 /code-review 3): FindForProcessingAsync projects the 5 scalar inputs without tracking the parent
    // MortgageApplication, and UpdateAssessmentAsync on the already-tracked assessment leaves immutable columns
    // (Id, ApplicationId, CreatedAt, FailedAt) unmodified.
    [Fact]
    public async Task FindForProcessingAsync_does_not_track_application_and_update_modifies_only_changed_columns()
    {
        var seeded = await SeedPendingAsync();
        var assessedAt = Now();
        var expected = Calculator().Assess(_e1);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
            var repository = scope.ServiceProvider.GetRequiredService<IMortgageApplicationRepository>();

            var work = (await repository.FindForProcessingAsync(seeded.Id, Http.Ct))!;

            Assert.Equal(_e1, work.Inputs);
            Assert.Empty(db.ChangeTracker.Entries<MortgageApplication>());
            var entry = Assert.Single(db.ChangeTracker.Entries<AffordabilityAssessment>());
            Assert.Equal(EntityState.Unchanged, entry.State);

            work.Assessment.Complete(expected, assessedAt);
            db.ChangeTracker.DetectChanges();
            var modifiedBeforeSave = entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).Order(StringComparer.Ordinal).ToList();
            Assert.Equal(["AssessedAt", "Result", "Status"], modifiedBeforeSave);

            // Guard: if UpdateAssessmentAsync called db.Assessments.Update on the tracked entity, every non-key property
            // (including ApplicationId, CreatedAt and FailedAt) would become IsModified = true before SaveChangesAsync.
            var modifiedOnSaving = new List<string>();
            db.SavingChanges += (_, _) =>
                modifiedOnSaving.AddRange(entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name));
            await repository.UpdateAssessmentAsync(work.Assessment, Http.Ct);
            Assert.Equal(["AssessedAt", "Result", "Status"], modifiedOnSaving.Order(StringComparer.Ordinal));
        }

        var reloaded = (await WithRepositoryAsync(r => r.FindAssessmentAsync(seeded.ApplicationId, seeded.Id, _owner, Http.Ct)))!;
        Assert.Equal(AssessmentStatus.Completed, reloaded.Status);
        Assert.Equal(assessedAt, reloaded.AssessedAt);
    }

    // P9-T8: UpdateAssessmentAsync still saves an assessment loaded in another scope (EntityState.Detached fallback).
    [Fact]
    public async Task UpdateAssessmentAsync_saves_a_detached_assessment()
    {
        var seeded = await SeedPendingAsync();
        var assessedAt = Now();
        var expected = Calculator().Assess(_e1);
        var detached = (await WithRepositoryAsync(r => r.FindAssessmentAsync(seeded.ApplicationId, seeded.Id, _owner, Http.Ct)))!;
        detached.Complete(expected, assessedAt);

        await WithRepositoryAsync(async r =>
        {
            await r.UpdateAssessmentAsync(detached, Http.Ct);
            return true;
        });

        var reloaded = (await WithRepositoryAsync(r => r.FindAssessmentAsync(seeded.ApplicationId, seeded.Id, _owner, Http.Ct)))!;
        Assert.Equal(AssessmentStatus.Completed, reloaded.Status);
        Assert.Equal(assessedAt, reloaded.AssessedAt);
    }

    // The processing load returns the stored inputs with a tracked assessment, which UpdateAssessmentAsync saves.
    private async Task FinishAsync(Guid assessmentId, Action<AffordabilityAssessment> finish)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMortgageApplicationRepository>();
        var work = (await repository.FindForProcessingAsync(assessmentId, Http.Ct))!;
        Assert.Equal(_e1, work.Inputs);
        finish(work.Assessment);
        await repository.UpdateAssessmentAsync(work.Assessment, Http.Ct);
    }

    private async Task<AffordabilityAssessment> SeedPendingAsync()
    {
        var application = MortgageApplication.Create(_owner, _e1, Now());
        var assessment = AffordabilityAssessment.CreatePending(application.Id, Now());
        await WithRepositoryAsync(async r =>
        {
            await r.AddAsync(application, Http.Ct);
            await r.AddAssessmentAsync(assessment, Http.Ct);
            return true;
        });
        return assessment;
    }

    private async Task<T> WithRepositoryAsync<T>(Func<IMortgageApplicationRepository, Task<T>> action)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IMortgageApplicationRepository>());
    }

    private AffordabilityCalculator Calculator() => _host.Services.GetRequiredService<AffordabilityCalculator>();

    // SQLite keeps DateTimeOffset as text with 100 ns precision, so a UTC clock value survives the round trip.
    private DateTimeOffset Now() => _host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
}
