using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MortgageApplications.IntegrationTests;

/// <summary>I4–I7: synchronous assessments end to end, with the golden numbers from rules section 8 (not recomputed).</summary>
public sealed class AssessmentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // I4: golden E1, every output and check, scale 2 in the raw JSON, and the Location resolves to the same body.
    [Fact]
    public async Task Post_assessment_E1_returns_201_approved_with_golden_values()
    {
        var applicationId = await _client.CreateApplicationAsync();

        var response = await _client.PostJsonAsync($"/api/applications/{applicationId}/assessments", "");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var text = await response.BodyAsync();
        using var body = JsonDocument.Parse(text);
        var root = body.RootElement;
        var assessmentId = root.GetProperty("id").GetGuid();
        Assert.Equal($"/api/applications/{applicationId}/assessments/{assessmentId}", response.Headers.Location?.OriginalString);
        Assert.Equal(applicationId, root.GetProperty("applicationId").GetString());
        Assert.Equal("Completed", root.GetProperty("status").GetString());
        Assert.Equal(TimeSpan.Zero, root.GetProperty("assessedAt").GetDateTimeOffset().Offset);
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

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(text), JsonNode.Parse(await fetched.BodyAsync())));
    }

    // I5: golden E2; Declined is a 201 result, not an error.
    [Fact]
    public async Task Post_assessment_E2_returns_201_declined_on_ltv()
    {
        var applicationId = await _client.CreateApplicationAsync(Http.BodyWith("""{"depositAmount":20000.00}"""));

        var response = await _client.PostJsonAsync($"/api/applications/{applicationId}/assessments", "");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.BodyAsync());
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
        var created = await _client.PostJsonAsync($"/api/applications/{applicationId}/assessments", "");

        var fetched = await _client.GetPathAsync(created.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        using var body = JsonDocument.Parse(await fetched.BodyAsync());
        var result = body.RootElement.GetProperty("result");
        Assert.Equal(100000000.00m, result.GetProperty("loanAmount").GetDecimal());
        Assert.Equal(100.00m, result.GetProperty("loanToValuePercent").GetDecimal());
        Assert.Equal(1933280.15m, result.GetProperty("monthlyPayment").GetDecimal());
        Assert.Equal(3519936183.53m, result.GetProperty("debtToIncomePercent").GetDecimal());
        Assert.Equal(100000000.00m, result.GetProperty("loanToIncomeMultiple").GetDecimal());
        Assert.Equal(0.00m, result.GetProperty("maxBorrowing").GetDecimal());
        Assert.Equal("Declined", result.GetProperty("decision").GetString());
    }

    // I7: not idempotent (P3-D4): each POST is a new assessment.
    [Fact]
    public async Task Post_assessment_twice_creates_two_assessments()
    {
        var applicationId = await _client.CreateApplicationAsync();

        var first = await _client.PostJsonAsync($"/api/applications/{applicationId}/assessments", "");
        var second = await _client.PostJsonAsync($"/api/applications/{applicationId}/assessments", "");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.NotEqual(first.Headers.Location, second.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetPathAsync(first.Headers.Location!.OriginalString)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetPathAsync(second.Headers.Location!.OriginalString)).StatusCode);
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
