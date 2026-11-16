using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;
using MortgageApplications.UnitTests.Affordability;

namespace MortgageApplications.UnitTests.Applications;

/// <summary>
/// U1–U8 and the Phase 6 cases (U-A, U-S, U-P): the use case takes the owner and the time from its callers, scopes
/// every caller read to the owner, saves a Pending assessment before it enqueues it, and processes queued IDs.
/// </summary>
public sealed class MortgageApplicationServiceTests
{
    private const string _ownerA = "owner-a";
    private const string _ownerB = "owner-b";

    private static readonly DateTimeOffset _now = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _later = _now.AddSeconds(3);

    private readonly FakeRepository _repository = new();
    private readonly FakeAssessmentQueue _queue = new();
    private readonly FixedTimeProvider _clock = new(_now);
    private readonly MortgageApplicationService _service;

    public MortgageApplicationServiceTests()
    {
        _service = new MortgageApplicationService(_repository, TestData.Calculator(), _queue, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // U1
    [Fact]
    public async Task CreateAsync_stores_application_with_caller_owner_and_clock_time()
    {
        var application = await _service.CreateAsync(_ownerA, TestData.E1, Ct);

        var stored = Assert.Single(_repository.Applications);
        Assert.Same(application, stored);
        Assert.Equal(_ownerA, stored.OwnerId);
        Assert.Equal(_now, stored.CreatedAt);
        Assert.Equal(TestData.E1, stored.ToInputs());
    }

    [Fact]
    public async Task GetAsync_own_application_returns_it()
    {
        var created = await _service.CreateAsync(_ownerA, TestData.E1, Ct);

        var found = await _service.GetAsync(_ownerA, created.Id, Ct);

        Assert.Same(created, found);
    }

    // U-A1 (was U2): the POST only saves a Pending assessment and queues it; the worker computes the result.
    [Fact]
    public async Task AssessAsync_saves_pending_assessment_and_enqueues_its_id()
    {
        var application = await _service.CreateAsync(_ownerA, TestData.E1, Ct);

        var assessment = await _service.AssessAsync(_ownerA, application.Id, Ct);

        var stored = Assert.Single(_repository.Assessments);
        Assert.Same(assessment, stored);
        Assert.Equal(application.Id, stored.ApplicationId);
        Assert.Equal(AssessmentStatus.Pending, stored.Status);
        Assert.Equal(_now, stored.CreatedAt);
        Assert.Null(stored.Result);
        Assert.Null(stored.AssessedAt);
        Assert.Null(stored.FailedAt);
        Assert.Equal([stored.Id], _queue.Enqueued);
    }

    // U-A2: save, then enqueue. A crash between the two leaves a Pending row the worker re-enqueues at startup.
    [Fact]
    public async Task AssessAsync_saves_before_it_enqueues()
    {
        var savedWhenEnqueued = false;
        var queue = new FakeAssessmentQueue(id => savedWhenEnqueued = _repository.Assessments.Any(a => a.Id == id));
        var service = new MortgageApplicationService(_repository, TestData.Calculator(), queue, _clock);
        var application = await service.CreateAsync(_ownerA, TestData.E1, Ct);

        await service.AssessAsync(_ownerA, application.Id, Ct);

        Assert.Single(queue.Enqueued);
        Assert.True(savedWhenEnqueued);
    }

    [Fact]
    public async Task GetAssessmentAsync_own_assessment_returns_it()
    {
        var application = await _service.CreateAsync(_ownerA, TestData.E1, Ct);
        var assessment = await _service.AssessAsync(_ownerA, application.Id, Ct);

        var found = await _service.GetAssessmentAsync(_ownerA, application.Id, assessment!.Id, Ct);

        Assert.Same(assessment, found);
    }

    // U5
    [Fact]
    public async Task GetAsync_other_owner_returns_null()
    {
        var application = await _service.CreateAsync(_ownerA, TestData.E1, Ct);

        var found = await _service.GetAsync(_ownerB, application.Id, Ct);

        Assert.Null(found);
    }

    // U6 + U-S1
    [Fact]
    public async Task AssessAsync_other_owner_returns_null_saves_nothing_and_queues_nothing()
    {
        var application = await _service.CreateAsync(_ownerA, TestData.E1, Ct);

        var assessment = await _service.AssessAsync(_ownerB, application.Id, Ct);

        Assert.Null(assessment);
        Assert.Empty(_repository.Assessments);
        Assert.Empty(_queue.Enqueued);
    }

    // U-S1
    [Fact]
    public async Task AssessAsync_unknown_application_returns_null_saves_nothing_and_queues_nothing()
    {
        var assessment = await _service.AssessAsync(_ownerA, Guid.CreateVersion7(_now), Ct);

        Assert.Null(assessment);
        Assert.Empty(_repository.Assessments);
        Assert.Empty(_queue.Enqueued);
    }

    // U7
    [Fact]
    public async Task GetAssessmentAsync_under_wrong_application_returns_null()
    {
        var first = await _service.CreateAsync(_ownerA, TestData.E1, Ct);
        var second = await _service.CreateAsync(_ownerA, TestData.E1, Ct);
        var assessment = await _service.AssessAsync(_ownerA, first.Id, Ct);

        var found = await _service.GetAssessmentAsync(_ownerA, second.Id, assessment!.Id, Ct);

        Assert.Null(found);
    }

    // U8
    [Fact]
    public async Task GetAssessmentAsync_other_owner_returns_null()
    {
        var application = await _service.CreateAsync(_ownerA, TestData.E1, Ct);
        var assessment = await _service.AssessAsync(_ownerA, application.Id, Ct);

        var found = await _service.GetAssessmentAsync(_ownerB, application.Id, assessment!.Id, Ct);

        Assert.Null(found);
    }

    // U-P1 (was U2): E1 golden values, the processing time from the clock and the rules version are saved.
    [Fact]
    public async Task ProcessAssessmentAsync_E1_completes_with_golden_values_and_clock_time()
    {
        var assessment = await PendingAssessmentAsync(TestData.E1);
        _clock.Now = _later;

        var outcome = await _service.ProcessAssessmentAsync(assessment.Id, Ct);

        Assert.Equal(AssessmentProcessingOutcome.Completed, outcome.Outcome);
        Assert.Same(assessment, Assert.Single(_repository.Updates));
        Assert.Equal(AssessmentStatus.Completed, assessment.Status);
        Assert.Equal(_now, assessment.CreatedAt);
        Assert.Equal(_later, assessment.AssessedAt);
        Assert.Null(assessment.FailedAt);
        var result = assessment.Result!;
        Assert.Equal("1.0", result.RulesVersion);
        Assert.Equal(0.06m, result.AssessmentRate);
        Assert.Equal(240_000.00m, result.LoanAmount);
        Assert.Equal(80.00m, result.LoanToValuePercent);
        Assert.Equal(1_438.92m, result.MonthlyPayment);
        Assert.Equal(33.02m, result.DebtToIncomePercent);
        Assert.Equal(2.82m, result.LoanToIncomeMultiple);
        Assert.Equal(381_535.82m, result.MaxBorrowing);
        Assert.Equal(Decision.Approved, result.Decision);
    }

    // U-P2 (was U3): Declined is a saved result, not a failure.
    [Fact]
    public async Task ProcessAssessmentAsync_E2_completes_as_declined_not_failed()
    {
        var assessment = await PendingAssessmentAsync(GoldenCases.All["E2"].Inputs);

        var outcome = await _service.ProcessAssessmentAsync(assessment.Id, Ct);

        Assert.Equal(AssessmentProcessingOutcome.Completed, outcome.Outcome);
        Assert.Equal(AssessmentStatus.Completed, assessment.Status);
        Assert.Null(assessment.FailedAt);
        Assert.Equal(Decision.Declined, assessment.Result!.Decision);
        Assert.Equal(280_000.00m, assessment.Result.LoanAmount);
        Assert.Equal(93.33m, assessment.Result.LoanToValuePercent);
    }

    // U-P4: stored inputs that break today's rules make the calculator throw; the assessment is saved as Failed.
    [Fact]
    public async Task ProcessAssessmentAsync_calculation_error_saves_failed_without_throwing()
    {
        var assessment = await PendingAssessmentAsync(TestData.E1);
        _repository.StoredInputsOverride = TestData.E1 with { TermYears = 50 };
        _clock.Now = _later;

        var outcome = await _service.ProcessAssessmentAsync(assessment.Id, Ct);

        Assert.Equal(AssessmentProcessingOutcome.Failed, outcome.Outcome);
        Assert.Same(assessment, Assert.Single(_repository.Updates));
        Assert.Equal(AssessmentStatus.Failed, assessment.Status);
        Assert.Equal(_later, assessment.FailedAt);
        Assert.Null(assessment.Result);
        Assert.Null(assessment.AssessedAt);
    }

    // U-P5: idempotent. Invalid stored inputs prove the calculator is not called: it would throw, and Fail on a
    // finished assessment throws too.
    [Fact]
    public async Task ProcessAssessmentAsync_completed_assessment_is_skipped_unchanged()
    {
        var assessment = await PendingAssessmentAsync(TestData.E1);
        await _service.ProcessAssessmentAsync(assessment.Id, Ct);
        var result = assessment.Result;
        _repository.Updates.Clear();
        _repository.StoredInputsOverride = TestData.E1 with { TermYears = 50 };
        _clock.Now = _later;

        var outcome = await _service.ProcessAssessmentAsync(assessment.Id, Ct);

        Assert.Equal(AssessmentProcessingOutcome.Skipped, outcome.Outcome);
        Assert.Empty(_repository.Updates);
        Assert.Equal(AssessmentStatus.Completed, assessment.Status);
        Assert.Same(result, assessment.Result);
        Assert.Equal(_now, assessment.AssessedAt);
    }

    // U-P6
    [Fact]
    public async Task ProcessAssessmentAsync_failed_assessment_is_skipped_unchanged()
    {
        var assessment = await PendingAssessmentAsync(TestData.E1);
        _repository.StoredInputsOverride = TestData.E1 with { TermYears = 50 };
        await _service.ProcessAssessmentAsync(assessment.Id, Ct);
        _repository.Updates.Clear();
        _repository.StoredInputsOverride = null;
        _clock.Now = _later;

        var outcome = await _service.ProcessAssessmentAsync(assessment.Id, Ct);

        Assert.Equal(AssessmentProcessingOutcome.Skipped, outcome.Outcome);
        Assert.Empty(_repository.Updates);
        Assert.Equal(AssessmentStatus.Failed, assessment.Status);
        Assert.Equal(_now, assessment.FailedAt);
        Assert.Null(assessment.Result);
    }

    // U-P7
    [Fact]
    public async Task ProcessAssessmentAsync_unknown_id_changes_nothing()
    {
        var outcome = await _service.ProcessAssessmentAsync(Guid.CreateVersion7(_now), Ct);

        Assert.Equal(AssessmentProcessingOutcome.NotFound, outcome.Outcome);
        Assert.Empty(_repository.Updates);
    }

    // U-P7 (Q4): an assessment whose application is gone (the foreign key cascades, so this should not happen).
    [Fact]
    public async Task ProcessAssessmentAsync_assessment_without_application_changes_nothing()
    {
        var assessment = await PendingAssessmentAsync(TestData.E1);
        _repository.Applications.Clear();

        var outcome = await _service.ProcessAssessmentAsync(assessment.Id, Ct);

        Assert.Equal(AssessmentProcessingOutcome.NotFound, outcome.Outcome);
        Assert.Empty(_repository.Updates);
        Assert.Equal(AssessmentStatus.Pending, assessment.Status);
    }

    // U-P10 (Q5): only the calculation is caught. A failed save reaches the worker, which logs it; the row stays
    // Pending in the database and the next startup re-enqueue retries it.
    [Fact]
    public async Task ProcessAssessmentAsync_save_failure_propagates()
    {
        var assessment = await PendingAssessmentAsync(TestData.E1);
        var failure = new InvalidOperationException("database unavailable");
        _repository.UpdateFailure = failure;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ProcessAssessmentAsync(assessment.Id, Ct));

        Assert.Same(failure, thrown);
        // One save attempt, of the Completed assessment; the use case does not fall back to Fail.
        Assert.Equal(1, _repository.UpdateAttempts);
        Assert.Equal(AssessmentStatus.Completed, assessment.Status);
        Assert.Null(assessment.FailedAt);
    }

    // R4: a clock that has moved back since the assessment was saved (time sync) must not make Complete throw;
    // the finish time is the creation time instead.
    [Fact]
    public async Task ProcessAssessmentAsync_clock_behind_created_at_completes_at_created_at()
    {
        var assessment = await PendingAssessmentAsync(TestData.E1);
        _clock.Now = _now.AddSeconds(-5);

        var outcome = await _service.ProcessAssessmentAsync(assessment.Id, Ct);

        Assert.Equal(AssessmentProcessingOutcome.Completed, outcome.Outcome);
        Assert.Equal(AssessmentStatus.Completed, assessment.Status);
        Assert.Equal(_now, assessment.AssessedAt);
    }

    // R4: the same for the Failed path.
    [Fact]
    public async Task ProcessAssessmentAsync_clock_behind_created_at_fails_at_created_at()
    {
        var assessment = await PendingAssessmentAsync(TestData.E1);
        _repository.StoredInputsOverride = TestData.E1 with { TermYears = 50 };
        _clock.Now = _now.AddSeconds(-5);

        var outcome = await _service.ProcessAssessmentAsync(assessment.Id, Ct);

        Assert.Equal(AssessmentProcessingOutcome.Failed, outcome.Outcome);
        Assert.Equal(AssessmentStatus.Failed, assessment.Status);
        Assert.Equal(_now, assessment.FailedAt);
    }

    // U-P11: the startup re-enqueue lists only Pending assessments.
    [Fact]
    public async Task ListPendingAssessmentIdsAsync_returns_only_pending_ids()
    {
        var completed = await PendingAssessmentAsync(TestData.E1);
        await _service.ProcessAssessmentAsync(completed.Id, Ct);
        var failed = await PendingAssessmentAsync(TestData.E1);
        _repository.StoredInputsOverride = TestData.E1 with { TermYears = 50 };
        await _service.ProcessAssessmentAsync(failed.Id, Ct);
        var pending = await PendingAssessmentAsync(TestData.E1);

        var ids = await _service.ListPendingAssessmentIdsAsync(Ct);

        Assert.Equal([pending.Id], ids);
    }

    // Phase 5 + Phase 9 P9-T1 (worker telemetry): a Completed result reports the application ID, decision and stored
    // rules version, nothing else.
    [Theory]
    [MemberData(nameof(DecisionCases))]
    public async Task ProcessAssessmentAsync_completed_reports_application_id_and_decision(string golden, Decision decision)
    {
        var assessment = await PendingAssessmentAsync(GoldenCases.All[golden].Inputs);

        var outcome = await _service.ProcessAssessmentAsync(assessment.Id, Ct);

        Assert.Equal(new AssessmentProcessingResult(AssessmentProcessingOutcome.Completed, assessment.ApplicationId, decision, "1.0"), outcome);
    }

    public static TheoryData<string, Decision> DecisionCases => new() { { "E1", Decision.Approved }, { "E2", Decision.Declined } };

    // Phase 5: a Failed result reports the application ID and no decision.
    [Fact]
    public async Task ProcessAssessmentAsync_failed_reports_application_id_without_decision()
    {
        var assessment = await PendingAssessmentAsync(TestData.E1);
        _repository.StoredInputsOverride = TestData.E1 with { TermYears = 50 };

        var outcome = await _service.ProcessAssessmentAsync(assessment.Id, Ct);

        Assert.Equal(new AssessmentProcessingResult(AssessmentProcessingOutcome.Failed, assessment.ApplicationId, null), outcome);
    }

    // Phase 5: a Skipped result still knows its application; it reports no decision even when the row is Completed.
    [Fact]
    public async Task ProcessAssessmentAsync_skipped_reports_application_id_without_decision()
    {
        var assessment = await PendingAssessmentAsync(TestData.E1);
        await _service.ProcessAssessmentAsync(assessment.Id, Ct);

        var outcome = await _service.ProcessAssessmentAsync(assessment.Id, Ct);

        Assert.Equal(new AssessmentProcessingResult(AssessmentProcessingOutcome.Skipped, assessment.ApplicationId, null), outcome);
    }

    // Phase 5: NotFound reports neither an application ID nor a decision.
    [Fact]
    public async Task ProcessAssessmentAsync_unknown_id_reports_no_application_id()
    {
        var outcome = await _service.ProcessAssessmentAsync(Guid.CreateVersion7(_now), Ct);

        Assert.Equal(new AssessmentProcessingResult(AssessmentProcessingOutcome.NotFound, null, null), outcome);
    }

    private async Task<AffordabilityAssessment> PendingAssessmentAsync(AffordabilityInputs inputs)
    {
        var application = await _service.CreateAsync(_ownerA, inputs, Ct);
        return (await _service.AssessAsync(_ownerA, application.Id, Ct))!;
    }
}
