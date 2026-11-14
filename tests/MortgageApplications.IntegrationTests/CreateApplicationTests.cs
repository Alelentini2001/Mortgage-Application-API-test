using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Api.Identity;

namespace MortgageApplications.IntegrationTests;

/// <summary>I1–I3, I37: create and read back an application.</summary>
public sealed class CreateApplicationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // I1
    [Fact]
    public async Task Post_valid_application_returns_201_with_location_and_body()
    {
        var response = await _client.PostJsonAsync("/api/applications", Http.BaseBody);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.ContentType()?.MediaType);
        using var body = JsonDocument.Parse(await response.BodyAsync());
        var id = body.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal($"/api/applications/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(85000.00m, body.RootElement.GetProperty("annualIncome").GetDecimal());
        Assert.Equal(900.00m, body.RootElement.GetProperty("monthlyExpenses").GetDecimal());
        Assert.Equal(60000.00m, body.RootElement.GetProperty("depositAmount").GetDecimal());
        Assert.Equal(300000.00m, body.RootElement.GetProperty("propertyValue").GetDecimal());
        Assert.Equal(30, body.RootElement.GetProperty("termYears").GetInt32());
        Assert.Equal(TimeSpan.Zero, body.RootElement.GetProperty("createdAt").GetDateTimeOffset().Offset);
    }

    [Fact]
    public async Task Post_then_get_location_returns_200_with_the_same_body()
    {
        var created = await _client.PostJsonAsync("/api/applications", Http.BaseBody);

        var fetched = await _client.GetPathAsync(created.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(await created.BodyAsync(), await fetched.BodyAsync());
    }

    // I2: money shows 2 dp in the raw JSON on create and after the SQLite round trip.
    [Fact]
    public async Task Post_and_get_show_money_with_two_decimal_places()
    {
        var created = await _client.PostJsonAsync(
            "/api/applications",
            """{"annualIncome":85000.5,"monthlyExpenses":900,"depositAmount":60000,"propertyValue":300000.1,"termYears":30}""");
        var fetched = await _client.GetPathAsync(created.Headers.Location!.OriginalString);

        foreach (var text in new[] { await created.BodyAsync(), await fetched.BodyAsync() })
        {
            Assert.Contains("\"annualIncome\":85000.50", text, StringComparison.Ordinal);
            Assert.Contains("\"monthlyExpenses\":900.00", text, StringComparison.Ordinal);
            Assert.Contains("\"depositAmount\":60000.00", text, StringComparison.Ordinal);
            Assert.Contains("\"propertyValue\":300000.10", text, StringComparison.Ordinal);
            Assert.Contains("\"termYears\":30", text, StringComparison.Ordinal);
        }
    }

    // I3 + I37: the owner never appears in a response.
    [Fact]
    public async Task Post_and_get_never_return_the_owner()
    {
        var owner = factory.Services.GetRequiredService<ICurrentUser>().OwnerId;

        var created = await _client.PostJsonAsync("/api/applications", Http.BaseBody);
        var fetched = await _client.GetPathAsync(created.Headers.Location!.OriginalString);

        foreach (var text in new[] { await created.BodyAsync(), await fetched.BodyAsync() })
        {
            Assert.DoesNotContain("ownerId", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(owner, text, StringComparison.Ordinal);
        }
    }
}
