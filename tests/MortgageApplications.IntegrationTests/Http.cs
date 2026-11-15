using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MortgageApplications.IntegrationTests;

/// <summary>Request builders and shared asserts. Bodies are raw JSON text, so tests control the exact number text.</summary>
internal static class Http
{
    /// <summary>Golden E1 (85,000 / 900 / 60,000 / 300,000 / 30y).</summary>
    public const string BaseBody =
        """{"annualIncome":85000.00,"monthlyExpenses":900.00,"depositAmount":60000.00,"propertyValue":300000.00,"termYears":30}""";

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The base body with some properties replaced (for example <c>{"termYears":4}</c>) and one optionally removed.</summary>
    public static string BodyWith(string overridesJson, string? without = null)
    {
        var body = JsonNode.Parse(BaseBody)!.AsObject();
        foreach (var (name, value) in JsonNode.Parse(overridesJson)!.AsObject())
        {
            body[name] = value?.DeepClone();
        }

        if (without is not null)
        {
            body.Remove(without);
        }

        return body.ToJsonString();
    }

    public static string BodyWithout(string property) => BodyWith("{}", without: property);

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, string json) =>
        client.PostAsync(new Uri(url, UriKind.Relative), new StringContent(json, Encoding.UTF8, "application/json"), Ct);

    public static Task<HttpResponseMessage> GetPathAsync(this HttpClient client, string url) =>
        client.GetAsync(new Uri(url, UriKind.Relative), Ct);

    public static Task<string> BodyAsync(this HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(Ct);

    /// <summary>Creates an application from a body and returns its id.</summary>
    public static async Task<string> CreateApplicationAsync(this HttpClient client, string json = BaseBody)
    {
        var response = await client.PostJsonAsync("/api/applications", json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.BodyAsync());
        return document.RootElement.GetProperty("id").GetString()!;
    }

    /// <summary>Starts an assessment of an application (202 Accepted when the application is the caller's).</summary>
    public static Task<HttpResponseMessage> PostAssessmentAsync(this HttpClient client, string applicationId) =>
        client.PostJsonAsync($"/api/applications/{applicationId}/assessments", "");

    /// <summary>
    /// Polls an assessment until it is no longer Pending and returns that body. Every poll must be a 200 (P6-D4).
    /// Bounded (25 ms interval, 10 s timeout), so a worker that never runs fails the test instead of hanging it.
    /// </summary>
    public static async Task<string> PollUntilFinishedAsync(this HttpClient client, string location)
    {
        var last = "";
        await EventuallyAsync(
            async () =>
            {
                var response = await client.GetPathAsync(location);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                last = await response.BodyAsync();
                return Status(last) != "Pending";
            },
            () => $"assessment still Pending; last body: {last}");
        return last;
    }

    /// <summary>Re-checks a condition every 25 ms for up to 10 s; the message says what was awaited.</summary>
    public static async Task EventuallyAsync(Func<Task<bool>> condition, Func<string> failureMessage)
    {
        var timeout = TimeSpan.FromSeconds(10);
        var started = TimeProvider.System.GetTimestamp();
        while (!await condition())
        {
            if (TimeProvider.System.GetElapsedTime(started) > timeout)
            {
                Assert.Fail($"Timed out after {timeout.TotalSeconds} s: {failureMessage()}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), Ct);
        }
    }

    /// <summary>The <c>status</c> field of an assessment body.</summary>
    public static string Status(string assessmentJson)
    {
        using var document = JsonDocument.Parse(assessmentJson);
        return document.RootElement.GetProperty("status").GetString()!;
    }

    /// <summary>The <c>id</c> field of a response body.</summary>
    public static async Task<string> IdAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.BodyAsync());
        return document.RootElement.GetProperty("id").GetString()!;
    }

    /// <summary>
    /// I40: every error is a ProblemDetails (RFC 9457) with status, title and a non-empty traceId.
    /// Returns the parsed body for further asserts.
    /// </summary>
    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.BodyAsync());
        var root = document.RootElement.Clone();
        Assert.Equal((int)status, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        return root;
    }

    /// <summary>A 400 ValidationProblemDetails: returns the field names in its <c>errors</c> object.</summary>
    public static async Task<string[]> AssertValidationProblemAsync(this HttpResponseMessage response)
    {
        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors");
        Assert.All(errors.EnumerateObject(), e => Assert.NotEmpty(e.Value.EnumerateArray().Select(m => m.GetString())));
        return [.. errors.EnumerateObject().Select(e => e.Name).Order(StringComparer.Ordinal)];
    }

    public static MediaTypeHeaderValue? ContentType(this HttpResponseMessage response) => response.Content.Headers.ContentType;
}
