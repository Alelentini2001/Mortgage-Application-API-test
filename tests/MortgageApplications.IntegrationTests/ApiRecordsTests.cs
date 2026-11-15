using MortgageApplications.Api.Applications;
using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// Security L1: the API records carry personal financial data, so their ToString must not print it
/// (rules section 6), the same as the Core records. Phase 5 adds logging; this stops a later
/// "log the request" from leaking values.
/// </summary>
public sealed class ApiRecordsTests
{
    private const string _sentinel = "123456";

    [Fact]
    public void Request_ToString_hides_the_inputs()
    {
        var request = new CreateApplicationRequest
        {
            AnnualIncome = 123_456.78m,
            MonthlyExpenses = 123_456.78m,
            DepositAmount = 123_456.78m,
            PropertyValue = 123_456.78m,
            TermYears = 30,
        };

        Assert.DoesNotContain(_sentinel, request.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Application_response_ToString_hides_the_inputs()
    {
        var response = new ApplicationResponse(Guid.Empty, 123_456.78m, 123_456.78m, 123_456.78m, 123_456.78m, 30, DateTimeOffset.UnixEpoch);

        Assert.DoesNotContain(_sentinel, response.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Result_and_check_responses_ToString_hide_the_values()
    {
        var check = new CheckResponse("LTV", 123_456.78m, 90m, false);
        var result = new AssessmentResultResponse(
            123_456.78m, 123_456.78m, 123_456.78m, 123_456.78m, 123_456.78m, 123_456.78m,
            Decision.Declined, [check], "Declined: LTV 123456.78% is above the 90.00% limit.", 0.06m, "1.0");
        var assessment = new AssessmentResponse(Guid.Empty, Guid.Empty, AssessmentStatus.Completed, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, result);

        Assert.DoesNotContain(_sentinel, check.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_sentinel, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_sentinel, assessment.ToString(), StringComparison.Ordinal);
    }
}
