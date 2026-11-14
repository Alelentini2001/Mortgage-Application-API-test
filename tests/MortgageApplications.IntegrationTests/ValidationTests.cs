using System.Net;

namespace MortgageApplications.IntegrationTests;

/// <summary>
/// I9–I18, I20, I21, I28: every rules section 1 bound at the HTTP boundary. Each row is the E1 body with some
/// properties replaced; an invalid row must name exactly one field (camelCase, A1) in a 400 ValidationProblemDetails.
/// </summary>
public sealed class ValidationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // I9–I15: (overrides, the one field expected in errors).
    public static TheoryData<string, string> InvalidRows => new()
    {
        { """{"annualIncome":0.99}""", "annualIncome" },
        { """{"annualIncome":10000000.01}""", "annualIncome" },
        { """{"monthlyExpenses":-0.01}""", "monthlyExpenses" },
        { """{"monthlyExpenses":1000000.01}""", "monthlyExpenses" },
        { """{"propertyValue":999.99,"depositAmount":0}""", "propertyValue" },
        { """{"propertyValue":100000000.01,"depositAmount":0}""", "propertyValue" },
        { """{"depositAmount":-0.01}""", "depositAmount" },
        { """{"termYears":4}""", "termYears" },
        { """{"termYears":41}""", "termYears" },
        { """{"propertyValue":200000,"depositAmount":199000.01}""", "depositAmount" },
        { """{"depositAmount":300000}""", "depositAmount" },
        { """{"annualIncome":85000.001}""", "annualIncome" },
        { """{"monthlyExpenses":900.005}""", "monthlyExpenses" },
        { """{"depositAmount":60000.001}""", "depositAmount" },
        { """{"propertyValue":300000.001}""", "propertyValue" },
    };

    // I9–I15: exact limits and 2 dp values that are accepted. 85000.100 has scale 3 but value 85000.1.
    public static TheoryData<string> ValidRows =>
    [
        """{"annualIncome":1}""",
        """{"annualIncome":10000000}""",
        """{"monthlyExpenses":0}""",
        """{"monthlyExpenses":1000000}""",
        """{"propertyValue":1000,"depositAmount":0}""",
        """{"propertyValue":100000000,"depositAmount":0}""",
        """{"depositAmount":0}""",
        """{"termYears":5}""",
        """{"termYears":40}""",
        """{"propertyValue":200000,"depositAmount":199000}""",
        """{"annualIncome":85000.10}""",
        """{"annualIncome":85000.100}""",
    ];

    public static TheoryData<string> Fields =>
        ["annualIncome", "monthlyExpenses", "depositAmount", "propertyValue", "termYears"];

    // I28: sentinel values that must not come back in the response.
    public static TheoryData<string> SentinelRows =>
    [
        """{"annualIncome":-123456.789}""",
        """{"monthlyExpenses":-123456.789}""",
        """{"depositAmount":-123456.789}""",
        """{"propertyValue":-123456.789}""",
        """{"termYears":123456}""",
    ];

    [Theory]
    [MemberData(nameof(InvalidRows))]
    public async Task Post_value_outside_rules_returns_400_naming_only_that_field(string overrides, string field)
    {
        var response = await _client.PostJsonAsync("/api/applications", Http.BodyWith(overrides));

        var fields = await response.AssertValidationProblemAsync();

        Assert.Equal([field], fields);
    }

    [Theory]
    [MemberData(nameof(ValidRows))]
    public async Task Post_value_at_limit_returns_201(string overrides)
    {
        var response = await _client.PostJsonAsync("/api/applications", Http.BodyWith(overrides));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // I16: a missing field is a 400, never a silent 0.
    [Theory]
    [MemberData(nameof(Fields))]
    public async Task Post_missing_field_returns_400_naming_it(string field)
    {
        var response = await _client.PostJsonAsync("/api/applications", Http.BodyWithout(field));

        var fields = await response.AssertValidationProblemAsync();

        Assert.Equal([field], fields);
    }

    // I17: explicit null; also proves the validation metadata exists (missing metadata fails silently).
    [Theory]
    [MemberData(nameof(Fields))]
    public async Task Post_null_field_returns_400_naming_it(string field)
    {
        var response = await _client.PostJsonAsync("/api/applications", Http.BodyWith($$"""{"{{field}}":null}"""));

        var fields = await response.AssertValidationProblemAsync();

        Assert.Equal([field], fields);
    }

    // I18
    [Fact]
    public async Task Post_empty_object_returns_400_naming_all_five_fields()
    {
        var response = await _client.PostJsonAsync("/api/applications", "{}");

        var fields = await response.AssertValidationProblemAsync();

        Assert.Equal(["annualIncome", "depositAmount", "monthlyExpenses", "propertyValue", "termYears"], fields);
    }

    // I20: the Core rules (IValidatableObject) don't run while a [Required] error exists (A5).
    [Fact]
    public async Task Post_missing_field_hides_the_minimum_loan_error()
    {
        var body = Http.BodyWith("""{"depositAmount":300000}""", without: "annualIncome");

        var response = await _client.PostJsonAsync("/api/applications", body);

        Assert.Equal(["annualIncome"], await response.AssertValidationProblemAsync());
    }

    // I21: same for [Range] on termYears.
    [Fact]
    public async Task Post_term_out_of_range_hides_the_minimum_loan_error()
    {
        var response = await _client.PostJsonAsync("/api/applications", Http.BodyWith("""{"termYears":4,"depositAmount":300000}"""));

        Assert.Equal(["termYears"], await response.AssertValidationProblemAsync());
    }

    // I28
    [Theory]
    [MemberData(nameof(SentinelRows))]
    public async Task Post_invalid_value_is_not_echoed(string overrides)
    {
        var response = await _client.PostJsonAsync("/api/applications", Http.BodyWith(overrides));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("123456", await response.BodyAsync(), StringComparison.Ordinal);
    }
}
