using Microsoft.EntityFrameworkCore;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.Infrastructure.Persistence;

/// <summary>Applications and their assessments (one-to-many). Mappings: the <c>*Configuration</c> classes next to this one.</summary>
public sealed class MortgageApplicationsDbContext(DbContextOptions<MortgageApplicationsDbContext> options)
    : DbContext(options)
{
    public DbSet<MortgageApplication> Applications => Set<MortgageApplication>();

    public DbSet<AffordabilityAssessment> Assessments => Set<AffordabilityAssessment>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<decimal>().HaveConversion<MinimumScaleTwoConverter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MortgageApplicationsDbContext).Assembly);
}
