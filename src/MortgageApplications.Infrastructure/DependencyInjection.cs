using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MortgageApplications.Core.Applications;
using MortgageApplications.Infrastructure.Persistence;

namespace MortgageApplications.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The SQLite DbContext (scoped) and the repository behind the Core use case.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<MortgageApplicationsDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IMortgageApplicationRepository, EfMortgageApplicationRepository>();
        return services;
    }
}
