using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;
using MortgageApplications.UnitTests.Affordability;

namespace MortgageApplications.UnitTests.Applications;

/// <summary>U1–U3, U5–U8: the use case takes the owner and the time from its callers, and scopes every read to the owner.</summary>
public sealed class MortgageApplicationServiceTests
{
    private const string _ownerA = "owner-a";
    private const string _ownerB = "owner-b";

    private static readonly DateTimeOffset _now = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeRepository _repository = new();
    private readonly MortgageApplicationService _service;

    public MortgageApplicationServiceTests()
    {
        _service = new MortgageApplicationService(_repository, TestData.Calculator(), new FixedTimeProvider(_now));
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

    // U2: E1 golden values, the clock time and the rules version are stored.
    [Fact]
    public async Task AssessAsync_E1_stores_completed_assessment_with_clock_time_and_rules_version()
    {
        var application = await _service.CreateAsync(_ownerA, TestData.E1, Ct);

        var assessment = await _service.AssessAsync(_ownerA, application.Id, Ct);

        var stored = Assert.Single(_repository.Assessments);
        Assert.Same(assessment, stored);
        Assert.Equal(application.Id, stored.ApplicationId);
        Assert.Equal(AssessmentStatus.Completed, stored.Status);
        Assert.Equal(_now, stored.CreatedAt);
        Assert.Equal(_now, stored.AssessedAt);
        var result = stored.Result!;
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

    // U3: Declined is a stored result, not an error.
    [Fact]
    public async Task AssessAsync_E2_stores_declined_assessment()
    {
        var application = await _service.CreateAsync(_ownerA, GoldenCases.All["E2"].Inputs, Ct);

        var assessment = await _service.AssessAsync(_ownerA, application.Id, Ct);

        Assert.Equal(AssessmentStatus.Completed, assessment!.Status);
        Assert.Equal(Decision.Declined, assessment.Result!.Decision);
        Assert.Equal(280_000.00m, assessment.Result.LoanAmount);
        Assert.Equal(93.33m, assessment.Result.LoanToValuePercent);
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

    // U6
    [Fact]
    public async Task AssessAsync_other_owner_returns_null_and_saves_nothing()
    {
        var application = await _service.CreateAsync(_ownerA, TestData.E1, Ct);

        var assessment = await _service.AssessAsync(_ownerB, application.Id, Ct);

        Assert.Null(assessment);
        Assert.Empty(_repository.Assessments);
    }

    [Fact]
    public async Task AssessAsync_unknown_application_returns_null_and_saves_nothing()
    {
        var assessment = await _service.AssessAsync(_ownerA, Guid.CreateVersion7(_now), Ct);

        Assert.Null(assessment);
        Assert.Empty(_repository.Assessments);
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
}
