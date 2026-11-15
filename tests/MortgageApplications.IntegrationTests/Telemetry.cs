using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.IntegrationTests;

/// <summary>Shared steps of the Phase 5 telemetry tests.</summary>
internal static class Telemetry
{
    public const string Counter = "mortgage.assessments.processed";
    public const string Histogram = "mortgage.assessment.duration";

    /// <summary>
    /// Sentinel inputs that appear nowhere else (I-13, I-14): any of their digits in telemetry is a leak.
    /// </summary>
    public const string SentinelBody =
        """{"annualIncome":85123.45,"monthlyExpenses":912.34,"depositAmount":61234.56,"propertyValue":301234.56,"termYears":30}""";

    /// <summary>Creates an application, starts an assessment and waits until it is finished.</summary>
    public static async Task<(string ApplicationId, Guid AssessmentId, string Body)> AssessAsync(HttpClient client, string body)
    {
        var applicationId = await client.CreateApplicationAsync(body);
        var posted = await client.PostAssessmentAsync(applicationId);
        Assert.Equal(HttpStatusCode.Accepted, posted.StatusCode);
        var finished = await client.PollUntilFinishedAsync(posted.Headers.Location!.OriginalString);
        return (applicationId, Guid.Parse(await posted.IdAsync()), finished);
    }

    /// <summary>
    /// An assessment that becomes Failed: the stored term is set to one today's rules reject (as in I-13), so the
    /// calculator throws.
    /// </summary>
    public static async Task<(string ApplicationId, Guid AssessmentId)> AssessFailedAsync(
        IServiceProvider services, HttpClient client, string body)
    {
        var applicationId = await client.CreateApplicationAsync(body);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MortgageApplicationsDbContext>();
            await db.Applications
                .Where(a => a.Id == Guid.Parse(applicationId))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.TermYears, 50), Http.Ct);
        }

        var posted = await client.PostAssessmentAsync(applicationId);
        Assert.Equal("Failed", Http.Status(await client.PollUntilFinishedAsync(posted.Headers.Location!.OriginalString)));
        return (applicationId, Guid.Parse(await posted.IdAsync()));
    }
}
