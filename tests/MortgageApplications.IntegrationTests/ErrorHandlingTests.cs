using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.IntegrationTests;

/// <summary>I38, I39 (security S5): an unexpected exception is a generic 500 ProblemDetails in every environment (A4).</summary>
public sealed class ErrorHandlingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Marker = "SECRET-MARKER-123";

    public static TheoryData<string> Environments => ["Development", "Production"];

    [Theory]
    [MemberData(nameof(Environments))]
    public async Task Unhandled_exception_returns_500_without_exception_details(string environment)
    {
        using var throwing = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .ConfigureTestServices(services => services.AddScoped<IMortgageApplicationRepository, ThrowingRepository>()));
        var client = throwing.CreateClient();

        var response = await client.GetPathAsync($"/api/applications/{Guid.NewGuid()}");

        await response.AssertProblemAsync(HttpStatusCode.InternalServerError);
        var text = await response.BodyAsync();
        Assert.DoesNotContain(Marker, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", text, StringComparison.Ordinal);
        Assert.DoesNotContain("MortgageApplications.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", text, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingRepository : IMortgageApplicationRepository
    {
        public Task AddAsync(MortgageApplication application, CancellationToken cancellationToken) => throw Boom();

        public Task<MortgageApplication?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken) => throw Boom();

        public Task AddAssessmentAsync(AffordabilityAssessment assessment, CancellationToken cancellationToken) => throw Boom();

        public Task<AffordabilityAssessment?> FindAssessmentAsync(
            Guid applicationId, Guid assessmentId, string ownerId, CancellationToken cancellationToken) => throw Boom();

        private static InvalidOperationException Boom() => new($"{Marker} at C:\\secret\\path.cs:line 1");
    }
}
