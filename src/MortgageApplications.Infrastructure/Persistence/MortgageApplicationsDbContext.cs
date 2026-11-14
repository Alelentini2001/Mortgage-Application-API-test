using Microsoft.EntityFrameworkCore;

namespace MortgageApplications.Infrastructure.Persistence;

public sealed class MortgageApplicationsDbContext(DbContextOptions<MortgageApplicationsDbContext> options)
    : DbContext(options);
