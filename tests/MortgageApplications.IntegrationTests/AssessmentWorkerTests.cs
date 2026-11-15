using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MortgageApplications.Api.Assessments;
using MortgageApplications.Api.Identity;
using MortgageApplications.Core.Applications;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// I-4, I-7 to I-11, I-13, I-14, W-6: the background worker in a real host. Each test has its own factory (its own
/// database and logs). A <see cref="WorkerGate"/> holds the worker wherever a test must observe Pending or a full
/// queue, and every wait is bounded, so no test depends on a sleep. The tests that hold the worker also have a
/// test timeout, in case a held host never lets go.
/// </summary>
public sealed class AssessmentWorkerTests
{
    private const int _timeoutMs = 30_000;
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);

    // I-4 (P6-D4): GET of a Pending assessment is a 200 with status Pending, never a 202.
    [Fact(Timeout = _timeoutMs)]
    public async Task Get_assessment_while_pending_returns_200_with_pending_status()
    {
        await using var factory = new ApiFactory();
        var gate = new WorkerGate();
        var client = await StartAsync(factory, gate);
        var applicationId = await client.CreateApplicationAsync();
        var location = (await client.PostAssessmentAsync(applicationId)).Headers.Location!.OriginalString;
        await gate.Entered.WaitAsync(_wait, TestContext.Current.CancellationToken);

        var response = await client.GetPathAsync(location);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.BodyAsync();
        Assert.Equal("Pending", Http.Status(text));
        Assert.Contains("\"result\":null", text, StringComparison.Ordinal);
        gate.Open();
        Assert.Equal("Completed", Http.Status(await client.PollUntilFinishedAsync(location)));
    }

    // I-7 (Q3): Pending rows saved while no worker ran are completed when a host with the worker starts; a Completed
    // row is left as it was. The first host runs without the worker, which models a restart.
    [Fact]
    public async Task Startup_completes_pending_assessments_and_leaves_finished_ones()
    {
        await using var factory = new ApiFactory();
        string pendingLocation;
        string completedLocation;
        string completedBody;
        await using (var withoutWorker = factory.WithWebHostBuilder(_ => { }))
        {
            var seeding = withoutWorker.CreateClient();
            var applicationId = await seeding.CreateApplicationAsync();
            pendingLocation = (await seeding.PostAssessmentAsync(applicationId)).Headers.Location!.OriginalString;
            var completed = await seeding.PostAssessmentAsync(applicationId);
            completedLocation = completed.Headers.Location!.OriginalString;
            await ProcessDirectlyAsync(withoutWorker.Services, Guid.Parse(await completed.IdAsync()));
            completedBody = await (await seeding.GetPathAsync(completedLocation)).BodyAsync();
            Assert.Equal("Pending", Http.Status(await (await seeding.GetPathAsync(pendingLocation)).BodyAsync()));
            Assert.Equal("Completed", Http.Status(completedBody));
        }

        var client = factory.CreateClient();

        Assert.Equal("Completed", Http.Status(await client.PollUntilFinishedAsync(pendingLocation)));
        Assert.Equal(completedBody, await (await client.GetPathAsync(completedLocation)).BodyAsync());
    }

    // I-8: an ID queued twice is processed once; the second time is skipped without an error.
    [Fact]
    public async Task Same_id_queued_twice_is_processed_once()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await WaitForStartupRequeueAsync(factory);
        var applicationId = await client.CreateApplicationAsync();
        var posted = await client.PostAssessmentAsync(applicationId);
        var location = posted.Headers.Location!.OriginalString;
        var first = await client.PollUntilFinishedAsync(location);

        await factory.Services.GetRequiredService<IAssessmentQueue>().EnqueueAsync(Guid.Parse(await posted.IdAsync()), Http.Ct);

        // One item at a time, in order: once a later item completes, the duplicate has been handled.
        var marker = await client.PostAssessmentAsync(applicationId);
        await client.PollUntilFinishedAsync(marker.Headers.Location!.OriginalString);
        Assert.Equal(first, await (await client.GetPathAsync(location)).BodyAsync());
        AssertNoWorkerWarningsOrErrors(factory);
    }

    // I-9 (P6-D2): with capacity 1 and the worker held, the third POST waits for room instead of dropping its ID;
    // after release all three complete.
    [Fact(Timeout = _timeoutMs)]
    public async Task Full_queue_makes_the_post_wait_and_drops_nothing()
    {
        await using var factory = new ApiFactory();
        var gate = new WorkerGate();
        var (host, client) = await StartWithCapacityOneAsync(factory, gate);
        var applicationId = await client.CreateApplicationAsync();
        var first = await client.PostAssessmentAsync(applicationId);
        await gate.Entered.WaitAsync(_wait, TestContext.Current.CancellationToken);
        var second = await client.PostAssessmentAsync(applicationId);

        var third = client.PostAssessmentAsync(applicationId);

        // The third row is saved before the enqueue, so once it exists the POST has reached the full queue.
        await Http.EventuallyAsync(async () => await CountAssessmentsAsync(host.Services) == 3, () => "third assessment saved");
        // Review R9: the only slot is taken (by the second ID), so the third POST can only be waiting in WriteAsync.
        var queue = host.Services.GetRequiredService<ChannelAssessmentQueue>();
        await Http.EventuallyAsync(() => Task.FromResult(queue.Reader.Count == 1), () => "one ID waiting in the queue");
        Assert.False(third.IsCompleted);
        gate.Open();
        var thirdResponse = await third.WaitAsync(_wait, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, thirdResponse.StatusCode);
        foreach (var response in new[] { first, second, thirdResponse })
        {
            Assert.Equal("Completed", Http.Status(await client.PollUntilFinishedAsync(response.Headers.Location!.OriginalString)));
        }
    }

    // I-10: a client that gives up while its POST waits for room leaves a saved Pending row that is never queued;
    // a restarted host completes it (startup re-enqueue).
    [Fact(Timeout = _timeoutMs)]
    public async Task Cancelled_waiting_post_leaves_pending_row_that_a_restart_completes()
    {
        await using var factory = new ApiFactory();
        var gate = new WorkerGate();
        var (host, client) = await StartWithCapacityOneAsync(factory, gate);
        var applicationId = await client.CreateApplicationAsync();
        var first = await client.PostAssessmentAsync(applicationId);
        await gate.Entered.WaitAsync(_wait, TestContext.Current.CancellationToken);
        var second = await client.PostAssessmentAsync(applicationId);
        using var giveUp = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var third = client.PostAsync(
            new Uri($"/api/applications/{applicationId}/assessments", UriKind.Relative),
            new StringContent("", Encoding.UTF8, "application/json"),
            giveUp.Token);
        await Http.EventuallyAsync(async () => await CountAssessmentsAsync(host.Services) == 3, () => "third assessment saved");

        await giveUp.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => third.WaitAsync(_wait, TestContext.Current.CancellationToken));
        var thirdId = (await AssessmentIdsAsync(host.Services))
            .Except([Guid.Parse(await first.IdAsync()), Guid.Parse(await second.IdAsync())])
            .Single();
        var thirdLocation = $"/api/applications/{applicationId}/assessments/{thirdId}";
        gate.Open();
        await client.PollUntilFinishedAsync(first.Headers.Location!.OriginalString);
        await client.PollUntilFinishedAsync(second.Headers.Location!.OriginalString);
        // One item at a time, in order: a later item completing proves the cancelled one was never queued.
        var marker = await client.PostAssessmentAsync(applicationId);
        await client.PollUntilFinishedAsync(marker.Headers.Location!.OriginalString);
        Assert.Equal("Pending", Http.Status(await (await client.GetPathAsync(thirdLocation)).BodyAsync()));
        await host.DisposeAsync();

        await using var restarted = factory.WithWebHostBuilder(_ => { }, runWorker: true);

        Assert.Equal("Completed", Http.Status(await restarted.CreateClient().PollUntilFinishedAsync(thirdLocation)));
    }

    // I-11: stopping the host while the worker is in the middle of an item leaves it Pending, not Failed.
    [Fact(Timeout = _timeoutMs)]
    public async Task Shutdown_while_processing_leaves_the_assessment_pending()
    {
        await using var factory = new ApiFactory();
        var gate = new WorkerGate();
        var host = factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(services => services.HoldWorkerAt(gate)), runWorker: true);
        var client = host.CreateClient();
        await WaitForStartupRequeueAsync(factory);
        var applicationId = await client.CreateApplicationAsync();
        var assessmentId = Guid.Parse(await (await client.PostAssessmentAsync(applicationId)).IdAsync());
        await gate.Entered.WaitAsync(_wait, TestContext.Current.CancellationToken);

        await host.DisposeAsync().AsTask().WaitAsync(_wait, TestContext.Current.CancellationToken);

        await using var reader = factory.WithWebHostBuilder(_ => { });
        using var scope = reader.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        var assessment = await db.Assessments.AsNoTracking().SingleAsync(a => a.Id == assessmentId, TestContext.Current.CancellationToken);
        Assert.Equal(AssessmentStatus.Pending, assessment.Status);
        Assert.Null(assessment.FailedAt);
        AssertNoWorkerWarningsOrErrors(factory);
    }

    // Review R5: stopping the host after Complete ran in memory but before the save leaves the row Pending, with no
    // result time and no failure time, and logs no error.
    [Fact(Timeout = _timeoutMs)]
    public async Task Shutdown_before_the_save_leaves_the_assessment_pending()
    {
        await using var factory = new ApiFactory();
        var gate = new WorkerGate();
        var host = factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(services => services.HoldWorkerAt(gate, WorkerGatePoint.BeforeSave)),
            runWorker: true);
        var client = host.CreateClient();
        await WaitForStartupRequeueAsync(factory);
        var applicationId = await client.CreateApplicationAsync();
        var assessmentId = Guid.Parse(await (await client.PostAssessmentAsync(applicationId)).IdAsync());
        await gate.Entered.WaitAsync(_wait, TestContext.Current.CancellationToken);

        await host.DisposeAsync().AsTask().WaitAsync(_wait, TestContext.Current.CancellationToken);

        await using var reader = factory.WithWebHostBuilder(_ => { });
        using var scope = reader.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        var assessment = await db.Assessments.AsNoTracking()
            .SingleAsync(a => a.Id == assessmentId, TestContext.Current.CancellationToken);
        Assert.Equal(AssessmentStatus.Pending, assessment.Status);
        Assert.Null(assessment.AssessedAt);
        Assert.Null(assessment.FailedAt);
        Assert.Null(assessment.Result);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Level >= LogLevel.Error);
    }

    // Review R1: a save that throws reaches the worker's per-item catch. The row stays Pending, the error log has the
    // assessment ID and no amounts or owner ID, and the worker goes on to complete the next item in the same host.
    [Fact]
    public async Task Failed_save_is_logged_leaves_the_row_pending_and_the_worker_continues()
    {
        const string sentinelBody =
            """{"annualIncome":85123.45,"monthlyExpenses":912.34,"depositAmount":61234.56,"propertyValue":301234.56,"termYears":30}""";
        await using var factory = new ApiFactory();
        await using var host = factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(services => services.FailFirstSave(new FailFirstSaveState())),
            runWorker: true);
        var client = host.CreateClient();
        await WaitForStartupRequeueAsync(factory);
        var posted = await client.PostAssessmentAsync(await client.CreateApplicationAsync(sentinelBody));
        var failedId = await posted.IdAsync();

        await Http.EventuallyAsync(
            () => Task.FromResult(factory.Logs.Entries.Any(e => e.EventName == "ProcessingError")),
            () => "the worker's processing error log");

        Assert.Equal("Pending", Http.Status(await (await client.GetPathAsync(posted.Headers.Location!.OriginalString)).BodyAsync()));
        var next = await client.PostAssessmentAsync(await client.CreateApplicationAsync());
        Assert.Equal("Completed", Http.Status(await client.PollUntilFinishedAsync(next.Headers.Location!.OriginalString)));
        var entries = factory.Logs.Entries;
        var error = Assert.Single(entries, e => e.EventName == "ProcessingError");
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Contains(failedId, error.Message, StringComparison.OrdinalIgnoreCase);
        var owner = host.Services.GetRequiredService<ICurrentUser>().OwnerId;
        foreach (var sentinel in new[] { "85123", "912.34", "61234", "301234", "240000", owner })
        {
            Assert.DoesNotContain(entries, e => e.Text.Contains(sentinel, StringComparison.Ordinal));
        }
    }

    // I-13 + I-14: an item whose stored inputs break today's rules becomes Failed (failedAt set, no result) and the
    // next item still completes. No log entry contains an input amount, the loan or the owner ID.
    [Fact]
    public async Task Failing_item_becomes_failed_the_next_completes_and_logs_hold_no_personal_data()
    {
        const string sentinelBody =
            """{"annualIncome":85123.45,"monthlyExpenses":912.34,"depositAmount":61234.56,"propertyValue":301234.56,"termYears":30}""";
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var badApplicationId = await client.CreateApplicationAsync(sentinelBody);
        // A row written before a rules change: a term the current rules reject, set directly in the database.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
            await db.Applications
                .Where(a => a.Id == Guid.Parse(badApplicationId))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.TermYears, 50), Http.Ct);
        }

        var failed = await client.PostAssessmentAsync(badApplicationId);
        var next = await client.PostAssessmentAsync(await client.CreateApplicationAsync());

        var failedBody = await client.PollUntilFinishedAsync(failed.Headers.Location!.OriginalString);
        var nextBody = await client.PollUntilFinishedAsync(next.Headers.Location!.OriginalString);
        Assert.Equal("Failed", Http.Status(failedBody));
        using (var document = System.Text.Json.JsonDocument.Parse(failedBody))
        {
            Assert.Equal(TimeSpan.Zero, document.RootElement.GetProperty("failedAt").GetDateTimeOffset().Offset);
        }

        Assert.Contains("\"assessedAt\":null", failedBody, StringComparison.Ordinal);
        Assert.Contains("\"result\":null", failedBody, StringComparison.Ordinal);
        Assert.Equal("Completed", Http.Status(nextBody));

        var failedId = await failed.IdAsync();
        var owner = factory.Services.GetRequiredService<ICurrentUser>().OwnerId;
        var entries = factory.Logs.Entries;
        Assert.Contains(entries, e => e.Category == typeof(AssessmentWorker).FullName
            && e.Level == LogLevel.Warning
            && e.Message.Contains(failedId, StringComparison.OrdinalIgnoreCase));
        foreach (var sentinel in new[] { "85123", "912.34", "61234", "301234", "240000", owner })
        {
            Assert.DoesNotContain(entries, e => e.Text.Contains(sentinel, StringComparison.Ordinal));
        }
    }

    // W-6: stopping an idle worker returns quickly, ends its loop normally and logs no error.
    [Fact]
    public async Task Stopping_an_idle_worker_returns_within_5_seconds_without_errors()
    {
        await using var factory = new ApiFactory();
        _ = factory.CreateClient();
        await WaitForStartupRequeueAsync(factory);
        var worker = factory.Services.GetServices<IHostedService>().OfType<AssessmentWorker>().Single();

        await worker.StopAsync(Http.Ct).WaitAsync(TimeSpan.FromSeconds(5), Http.Ct);

        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Level >= LogLevel.Error);
    }

    private static async Task<HttpClient> StartAsync(ApiFactory factory, WorkerGate gate)
    {
        var host = factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(services => services.HoldWorkerAt(gate)), runWorker: true);
        var client = host.CreateClient();
        await WaitForStartupRequeueAsync(factory);
        return client;
    }

    private static async Task<(WebApplicationFactory<Program> Host, HttpClient Client)> StartWithCapacityOneAsync(
        ApiFactory factory, WorkerGate gate)
    {
        var host = factory.WithWebHostBuilder(
            builder => builder
                .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Assessments:QueueCapacity"] = "1" }))
                .ConfigureTestServices(services => services.HoldWorkerAt(gate)),
            runWorker: true);
        var client = host.CreateClient();
        await WaitForStartupRequeueAsync(factory);
        return (host, client);
    }

    // The startup re-enqueue runs next to the first requests; waiting for it keeps a test's queue contents exact.
    private static Task WaitForStartupRequeueAsync(ApiFactory factory) =>
        Http.EventuallyAsync(
            () => Task.FromResult(factory.Logs.Entries.Any(e => e.EventName == "PendingRequeued")),
            () => "the worker's startup re-enqueue");

    private static async Task ProcessDirectlyAsync(IServiceProvider services, Guid assessmentId)
    {
        await using var scope = services.CreateAsyncScope();
        var outcome = await scope.ServiceProvider.GetRequiredService<MortgageApplicationService>()
            .ProcessAssessmentAsync(assessmentId, Http.Ct);
        Assert.Equal(AssessmentProcessingOutcome.Completed, outcome);
    }

    private static async Task<int> CountAssessmentsAsync(IServiceProvider services) =>
        (await AssessmentIdsAsync(services)).Count;

    private static async Task<List<Guid>> AssessmentIdsAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
        return await db.Assessments.Select(a => a.Id).ToListAsync(Http.Ct);
    }

    private static void AssertNoWorkerWarningsOrErrors(ApiFactory factory)
    {
        var entries = factory.Logs.Entries;
        Assert.DoesNotContain(entries, e => e.Level >= LogLevel.Error);
        Assert.DoesNotContain(entries, e => e.Category == typeof(AssessmentWorker).FullName && e.Level >= LogLevel.Warning);
    }
}
