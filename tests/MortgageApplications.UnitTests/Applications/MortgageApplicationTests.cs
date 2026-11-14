using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.UnitTests.Applications;

public sealed class MortgageApplicationTests
{
    public static TheoryData<string> BlankOwners => ["", "   "];

    // M1
    [Fact]
    public void Create_valid_inputs_keeps_owner_inputs_and_created_time()
    {
        var application = MortgageApplication.Create("owner-1", TestData.E1, TestData.CreatedAt);

        Assert.Equal("owner-1", application.OwnerId);
        Assert.Equal(85_000m, application.AnnualIncome);
        Assert.Equal(900m, application.MonthlyExpenses);
        Assert.Equal(60_000m, application.DepositAmount);
        Assert.Equal(300_000m, application.PropertyValue);
        Assert.Equal(30, application.TermYears);
        Assert.Equal(TestData.CreatedAt, application.CreatedAt);
    }

    [Fact]
    public void Create_assigns_a_version_7_id()
    {
        var application = MortgageApplication.Create("owner-1", TestData.E1, TestData.CreatedAt);

        Assert.NotEqual(Guid.Empty, application.Id);
        Assert.Equal(7, application.Id.Version);
    }

    // M2
    [Fact]
    public void Create_twice_gives_different_ids()
    {
        var first = MortgageApplication.Create("owner-1", TestData.E1, TestData.CreatedAt);
        var second = MortgageApplication.Create("owner-1", TestData.E1, TestData.CreatedAt);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void ToInputs_returns_the_stored_inputs()
    {
        var application = MortgageApplication.Create("owner-1", TestData.E1, TestData.CreatedAt);

        Assert.Equal(TestData.E1, application.ToInputs());
    }

    // M4
    [Theory]
    [MemberData(nameof(BlankOwners))]
    public void Create_blank_owner_throws(string ownerId)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => MortgageApplication.Create(ownerId, TestData.E1, TestData.CreatedAt));

        Assert.Equal("ownerId", ex.ParamName);
    }

    [Fact]
    public void Create_null_owner_throws_argument_null()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => MortgageApplication.Create(null!, TestData.E1, TestData.CreatedAt));

        Assert.Equal("ownerId", ex.ParamName);
    }

    [Fact]
    public void Create_null_inputs_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => MortgageApplication.Create("owner-1", null!, TestData.CreatedAt));
    }

    [Fact]
    public void Create_non_utc_time_throws()
    {
        var local = new DateTimeOffset(2026, 11, 14, 11, 30, 0, TimeSpan.FromHours(2));

        var ex = Assert.Throws<ArgumentException>(() => MortgageApplication.Create("owner-1", TestData.E1, local));

        Assert.Equal("createdAt", ex.ParamName);
    }

    // Review R1: Guid.CreateVersion7 would reject these with the value in its message.
    [Fact]
    public void Create_default_time_throws_without_the_value()
    {
        var ex = Assert.Throws<ArgumentException>(() => MortgageApplication.Create("owner-1", TestData.E1, default));

        Assert.Equal("createdAt", ex.ParamName);
        Assert.DoesNotContain("Actual value", ex.Message, StringComparison.Ordinal);
    }

    // Review N6: inputs are stored with scale 2 even when submitted as 100.500.
    [Fact]
    public void Create_stores_money_with_scale_2()
    {
        var inputs = TestData.E1 with { AnnualIncome = 100.500m, MonthlyExpenses = 0m };

        var application = MortgageApplication.Create("owner-1", inputs, TestData.CreatedAt);

        Assert.Equal(100.50m, application.AnnualIncome);
        TestData.AssertScale2(application.AnnualIncome);
        TestData.AssertScale2(application.MonthlyExpenses);
        TestData.AssertScale2(application.DepositAmount);
        TestData.AssertScale2(application.PropertyValue);
    }

    [Fact]
    public void Create_valid_inputs_can_be_assessed()
    {
        var application = MortgageApplication.Create("owner-1", TestData.E1, TestData.CreatedAt);

        var result = TestData.Calculator().Assess(application.ToInputs());

        Assert.Equal(Decision.Approved, result.Decision);
    }
}
