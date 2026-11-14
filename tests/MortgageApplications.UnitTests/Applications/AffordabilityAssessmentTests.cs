using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;
using MortgageApplications.UnitTests.Affordability;

namespace MortgageApplications.UnitTests.Applications;

/// <summary>Assessment lifecycle (A1–A5): Pending → Completed with a result, or Failed; only once.</summary>
public sealed class AffordabilityAssessmentTests
{
    private static readonly Guid _applicationId = Guid.CreateVersion7(TestData.CreatedAt);
    private static readonly DateTimeOffset _finishedAt = TestData.CreatedAt.AddSeconds(2);

    // A1
    [Fact]
    public void CreatePending_starts_pending_with_no_result()
    {
        var assessment = AffordabilityAssessment.CreatePending(_applicationId, TestData.CreatedAt);

        Assert.Equal(AssessmentStatus.Pending, assessment.Status);
        Assert.Null(assessment.Result);
        Assert.Null(assessment.AssessedAt);
        Assert.Null(assessment.FailedAt);
        Assert.Equal(_applicationId, assessment.ApplicationId);
        Assert.Equal(TestData.CreatedAt, assessment.CreatedAt);
        Assert.Equal(7, assessment.Id.Version);
    }

    [Fact]
    public void CreatePending_empty_application_id_throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => AffordabilityAssessment.CreatePending(Guid.Empty, TestData.CreatedAt));

        Assert.Equal("applicationId", ex.ParamName);
    }

    [Fact]
    public void CreatePending_non_utc_time_throws()
    {
        var local = TestData.CreatedAt.ToOffset(TimeSpan.FromHours(2));

        var ex = Assert.Throws<ArgumentException>(() => AffordabilityAssessment.CreatePending(_applicationId, local));

        Assert.Equal("createdAt", ex.ParamName);
    }

    [Fact]
    public void CreatePending_default_time_throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => AffordabilityAssessment.CreatePending(_applicationId, default));

        Assert.Equal("createdAt", ex.ParamName);
    }

    // Review R3: a hand-built result whose decision contradicts its checks is rejected.
    [Fact]
    public void Complete_result_with_decision_not_matching_checks_throws_and_stays_pending()
    {
        var assessment = Pending();
        var tampered = TestData.Calculator().Assess(TestData.E1) with { Decision = Decision.Declined };

        var ex = Assert.Throws<ArgumentException>(() => assessment.Complete(tampered, _finishedAt));

        Assert.Equal("result", ex.ParamName);
        Assert.Equal(AssessmentStatus.Pending, assessment.Status);
    }

    [Fact]
    public void Complete_result_without_three_checks_throws()
    {
        var assessment = Pending();
        var valid = TestData.Calculator().Assess(TestData.E1);
        var tampered = valid with { Checks = valid.Checks.Take(2).ToArray() };

        Assert.Throws<ArgumentException>(() => assessment.Complete(tampered, _finishedAt));
    }

    // A2: the result carries the rules version, rate and limits used.
    [Fact]
    public void Complete_pending_stores_result_and_time()
    {
        var assessment = Pending();
        var result = TestData.Calculator().Assess(TestData.E1);

        assessment.Complete(result, _finishedAt);

        Assert.Equal(AssessmentStatus.Completed, assessment.Status);
        Assert.Same(result, assessment.Result);
        Assert.Equal(_finishedAt, assessment.AssessedAt);
        Assert.Null(assessment.FailedAt);
        Assert.Equal("1.0", assessment.Result!.RulesVersion);
        Assert.Equal(0.06m, assessment.Result.AssessmentRate);
        Assert.Equal([90.00m, 45.00m, 4.50m], assessment.Result.Checks.Select(c => c.Limit));
    }

    // A3
    [Fact]
    public void Fail_pending_marks_failed_with_time_and_no_result()
    {
        var assessment = Pending();

        assessment.Fail(_finishedAt);

        Assert.Equal(AssessmentStatus.Failed, assessment.Status);
        Assert.Null(assessment.Result);
        Assert.Null(assessment.AssessedAt);
        Assert.Equal(_finishedAt, assessment.FailedAt);
    }

    // A4: no second state change; the first outcome is kept.
    [Fact]
    public void Complete_twice_throws_and_keeps_first_result()
    {
        var assessment = Pending();
        var first = TestData.Calculator().Assess(TestData.E1);
        assessment.Complete(first, _finishedAt);
        var second = TestData.Calculator().Assess(GoldenCases.All["E3"].Inputs);

        Assert.Throws<InvalidOperationException>(() => assessment.Complete(second, _finishedAt.AddSeconds(1)));

        Assert.Equal(AssessmentStatus.Completed, assessment.Status);
        Assert.Same(first, assessment.Result);
        Assert.Equal(_finishedAt, assessment.AssessedAt);
    }

    [Fact]
    public void Fail_after_complete_throws_and_keeps_completed()
    {
        var assessment = Pending();
        assessment.Complete(TestData.Calculator().Assess(TestData.E1), _finishedAt);

        Assert.Throws<InvalidOperationException>(() => assessment.Fail(_finishedAt));

        Assert.Equal(AssessmentStatus.Completed, assessment.Status);
        Assert.Null(assessment.FailedAt);
    }

    [Fact]
    public void Complete_after_fail_throws_and_keeps_failed()
    {
        var assessment = Pending();
        assessment.Fail(_finishedAt);

        Assert.Throws<InvalidOperationException>(
            () => assessment.Complete(TestData.Calculator().Assess(TestData.E1), _finishedAt));

        Assert.Equal(AssessmentStatus.Failed, assessment.Status);
        Assert.Null(assessment.Result);
    }

    [Fact]
    public void Fail_twice_throws()
    {
        var assessment = Pending();
        assessment.Fail(_finishedAt);

        Assert.Throws<InvalidOperationException>(() => assessment.Fail(_finishedAt.AddSeconds(1)));

        Assert.Equal(_finishedAt, assessment.FailedAt);
    }

    // A5
    [Fact]
    public void Complete_null_result_throws_and_stays_pending()
    {
        var assessment = Pending();

        Assert.Throws<ArgumentNullException>(() => assessment.Complete(null!, _finishedAt));

        Assert.Equal(AssessmentStatus.Pending, assessment.Status);
    }

    [Fact]
    public void Complete_before_created_time_throws_and_stays_pending()
    {
        var assessment = Pending();

        var ex = Assert.Throws<ArgumentException>(
            () => assessment.Complete(TestData.Calculator().Assess(TestData.E1), TestData.CreatedAt.AddSeconds(-1)));

        Assert.Equal("assessedAt", ex.ParamName);
        Assert.Equal(AssessmentStatus.Pending, assessment.Status);
    }

    [Fact]
    public void Fail_non_utc_time_throws_and_stays_pending()
    {
        var assessment = Pending();

        var ex = Assert.Throws<ArgumentException>(() => assessment.Fail(_finishedAt.ToOffset(TimeSpan.FromHours(2))));

        Assert.Equal("failedAt", ex.ParamName);
        Assert.Equal(AssessmentStatus.Pending, assessment.Status);
    }

    private static AffordabilityAssessment Pending() =>
        AffordabilityAssessment.CreatePending(_applicationId, TestData.CreatedAt);
}
