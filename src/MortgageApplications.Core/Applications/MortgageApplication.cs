using MortgageApplications.Core.Affordability;

namespace MortgageApplications.Core.Applications;

public sealed class MortgageApplication
{
    public Guid Id { get; }

    public string OwnerId { get; } = string.Empty;

    public decimal AnnualIncome { get; }

    public decimal MonthlyExpenses { get; }

    public decimal DepositAmount { get; }

    public decimal PropertyValue { get; }

    public int TermYears { get; }

    public DateTimeOffset CreatedAt { get; }

    public static MortgageApplication Create(string ownerId, AffordabilityInputs inputs, DateTimeOffset createdAt) =>
        throw new NotImplementedException();

    public AffordabilityInputs ToInputs() => throw new NotImplementedException();
}
