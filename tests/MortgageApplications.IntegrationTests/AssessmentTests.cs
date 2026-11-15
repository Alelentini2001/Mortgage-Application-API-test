using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Core.Affordability;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// I-1 to I-3, I4–I7: background assessments end to end. The POST returns 202 with a Pending body; polling the
/// <c>Location</c> reaches the golden numbers from rules section 8 (not recomputed). Polling is bounded (Http).
/// </summary>
public sealed class AssessmentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // I-1: 202, the exact Location, and a Pending body with explicit nulls in the raw JSON.
    [Fact]
    public async Task Post_assessment_returns_202_with_location_and_pending_body()
    {
        var applicationId = await _client.CreateApplicationAsync();

        var response = await _client.PostAssessmentAsync(applicationId);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("application/json", response.ContentType()?.MediaType);
        var text = await response.BodyAsync();
        using var body = JsonDocument.Parse(text);
        var root = body.RootElement;
        var assessmentId = root.GetProperty("id").GetGuid();
        Assert.Equal($"/api/applications/{applicationId}/assessments/{assessmentId}", response.Headers.Location?.OriginalString);
        Assert.Equal(applicationId, root.GetProperty("applicationId").GetString());
        Assert.Equal("Pending", root.GetProperty("status").GetString());
        Assert.Equal(TimeSpan.Zero, root.GetProperty("createdAt").GetDateTimeOffset().Offset);
        Assert.Contains("\"assessedAt\":null", text, StringComparison.Ordinal);
        Assert.Contains("\"failedAt\":null", text, StringComparison.Ordinal);
        Assert.Contains("\"result\":null", text, StringComparison.Ordinal);
    }

    // I-2 (was I4): golden E1, every output and check, scale 2 in the raw JSON, and the Location keeps giving the same body.
    [Fact]
    public async Task Post_assessment_E1_completes_approved_with_golden_values()
    {
        var applicationId = await _client.CreateApplicationAsync();
        var response = await _client.PostAssessmentAsync(applicationId);

        var text = await _client.PollUntilFinishedAsync(response.Headers.Location!.OriginalString);

        using var body = JsonDocument.Parse(text);
        var root = body.RootElement;
        Assert.Equal("Completed", root.GetProperty("status").GetString());
        Assert.Equal(applicationId, root.GetProperty("applicationId").GetString());
        Assert.Equal(TimeSpan.Zero, root.GetProperty("assessedAt").GetDateTimeOffset().Offset);
        Assert.True(root.GetProperty("assessedAt").GetDateTimeOffset() >= root.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Contains("\"failedAt\":null", text, StringComparison.Ordinal);
        var result = root.GetProperty("result");
        Assert.Equal(240000.00m, result.GetProperty("loanAmount").GetDecimal());
        Assert.Equal(80.00m, result.GetProperty("loanToValuePercent").GetDecimal());
        Assert.Equal(1438.92m, result.GetProperty("monthlyPayment").GetDecimal());
        Assert.Equal(33.02m, result.GetProperty("debtToIncomePercent").GetDecimal());
        Assert.Equal(2.82m, result.GetProperty("loanToIncomeMultiple").GetDecimal());
        Assert.Equal(381535.82m, result.GetProperty("maxBorrowing").GetDecimal());
        Assert.Equal("Approved", result.GetProperty("decision").GetString());
        Assert.Equal(
            "Approved: all 3 checks passed (LTV 80.00% ≤ 90.00%, DTI 33.02% ≤ 45.00%, LTI 2.82× ≤ 4.50×).",
            result.GetProperty("summary").GetString());
        Assert.Equal(0.06m, result.GetProperty("assessmentRate").GetDecimal());
        Assert.Equal("1.0", result.GetProperty("rulesVersion").GetString());
        Assert.Equal(
            [("LTV", 80.00m, 90.00m, true), ("DTI", 33.02m, 45.00m, true), ("LTI", 2.82m, 4.50m, true)],
            Checks(result));
        Assert.Contains("\"loanAmount\":240000.00", text, StringComparison.Ordinal);
        Assert.Contains("\"loanToValuePercent\":80.00", text, StringComparison.Ordinal);
        Assert.Contains("\"limit\":4.50", text, StringComparison.Ordinal);

        var fetched = await _client.GetPathAsync(response.Headers.Location!.OriginalString);

        // Review F1: compare the raw text, so a dropped trailing zero after the SQLite/JSON round trip fails
        // (JsonNode.DeepEquals treats 240000.0 and 240000.00 as equal).
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(text, await fetched.BodyAsync());
    }

    // I-3 (was I5): golden E2; Declined completes (it is a result, not a failure).
    [Fact]
    public async Task Post_assessment_E2_completes_declined_on_ltv()
    {
        var applicationId = await _client.CreateApplicationAsync(Http.BodyWith("""{"depositAmount":20000.00}"""));
        var response = await _client.PostAssessmentAsync(applicationId);

        var text = await _client.PollUntilFinishedAsync(response.Headers.Location!.OriginalString);

        using var body = JsonDocument.Parse(text);
        Assert.Equal("Completed", body.RootElement.GetProperty("status").GetString());
        Assert.Contains("\"failedAt\":null", text, StringComparison.Ordinal);
        var result = body.RootElement.GetProperty("result");
        Assert.Equal("Declined", result.GetProperty("decision").GetString());
        Assert.Equal(280000.00m, result.GetProperty("loanAmount").GetDecimal());
        Assert.Equal(93.33m, result.GetProperty("loanToValuePercent").GetDecimal());
        Assert.Equal(1678.74m, result.GetProperty("monthlyPayment").GetDecimal());
        Assert.Equal(36.41m, result.GetProperty("debtToIncomePercent").GetDecimal());
        Assert.Equal(3.29m, result.GetProperty("loanToIncomeMultiple").GetDecimal());
        Assert.Equal(381535.82m, result.GetProperty("maxBorrowing").GetDecimal());
        Assert.Equal("Declined: LTV 93.33% is above the 90.00% limit.", result.GetProperty("summary").GetString());
        Assert.Equal([false, true, true], Checks(result).Select(c => c.Passed));
    }

    // I6: golden E18; 10+ integer digits survive storage (rules section 8).
    [Fact]
    public async Task Post_assessment_E18_extreme_values_survive_the_round_trip()
    {
        var applicationId = await _client.CreateApplicationAsync(
            """{"annualIncome":1,"monthlyExpenses":1000000,"depositAmount":0,"propertyValue":100000000,"termYears":5}""");
        var created = await _client.PostAssessmentAsync(applicationId);

        var text = await _client.PollUntilFinishedAsync(created.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        Assert.Contains("\"debtToIncomePercent\":3519936183.53", text, StringComparison.Ordinal);
        Assert.Contains("\"loanToIncomeMultiple\":100000000.00", text, StringComparison.Ordinal);
        Assert.Contains("\"maxBorrowing\":0.00", text, StringComparison.Ordinal);
        using var body = JsonDocument.Parse(text);
        var result = body.RootElement.GetProperty("result");
        Assert.Equal(100000000.00m, result.GetProperty("loanAmount").GetDecimal());
        Assert.Equal(100.00m, result.GetProperty("loanToValuePercent").GetDecimal());
        Assert.Equal(1933280.15m, result.GetProperty("monthlyPayment").GetDecimal());
        Assert.Equal(3519936183.53m, result.GetProperty("debtToIncomePercent").GetDecimal());
        Assert.Equal(100000000.00m, result.GetProperty("loanToIncomeMultiple").GetDecimal());
        Assert.Equal(0.00m, result.GetProperty("maxBorrowing").GetDecimal());
        Assert.Equal("Declined", result.GetProperty("decision").GetString());
    }

    // I7 (Q12): not idempotent (P3-D4): each POST is a new assessment, and both complete.
    [Fact]
    public async Task Post_assessment_twice_creates_two_assessments()
    {
        var applicationId = await _client.CreateApplicationAsync();

        var first = await _client.PostAssessmentAsync(applicationId);
        var second = await _client.PostAssessmentAsync(applicationId);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.NotEqual(first.Headers.Location, second.Headers.Location);
        Assert.Equal("Completed", Http.Status(await _client.PollUntilFinishedAsync(first.Headers.Location!.OriginalString)));
        Assert.Equal("Completed", Http.Status(await _client.PollUntilFinishedAsync(second.Headers.Location!.OriginalString)));
    }

    // Review F3: a result loaded from the database keeps read-only checks, like one stored by Complete.
    [Fact]
    public async Task Loaded_result_checks_are_read_only()
    {
        var applicationId = await _client.CreateApplicationAsync();
        var created = await _client.PostAssessmentAsync(applicationId);
        var assessmentId = Guid.Parse(await created.IdAsync());
        await _client.PollUntilFinishedAsync(created.Headers.Location!.OriginalString);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();

        var assessment = await db.Assessments.SingleAsync(a => a.Id == assessmentId, Http.Ct);

        var checks = Assert.IsAssignableFrom<ICollection<CheckResult>>(assessment.Result!.Checks);
        Assert.True(checks.IsReadOnly);
    }

    private static (string Name, decimal Value, decimal Limit, bool Passed)[] Checks(JsonElement result) =>
    [
        .. result.GetProperty("checks").EnumerateArray().Select(c => (
            c.GetProperty("name").GetString()!,
            c.GetProperty("value").GetDecimal(),
            c.GetProperty("limit").GetDecimal(),
            c.GetProperty("passed").GetBoolean())),
    ];
}
