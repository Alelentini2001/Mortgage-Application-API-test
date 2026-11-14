using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MortgageApplications.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> to build the model for migrations, so the tools don't need to start the API
/// (which validates its options at startup). The connection string is never opened by <c>migrations add</c>.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MortgageApplicationsDbContext>
{
    public MortgageApplicationsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<MortgageApplicationsDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options);
}
