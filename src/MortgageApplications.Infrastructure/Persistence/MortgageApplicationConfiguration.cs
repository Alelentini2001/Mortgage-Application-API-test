using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.Infrastructure.Persistence;

/// <summary>
/// The entity has get-only properties (no public setters), which EF doesn't map by convention, so each one is
/// mapped here; EF loads it through the entity's private scalar constructor.
/// </summary>
internal sealed class MortgageApplicationConfiguration : IEntityTypeConfiguration<MortgageApplication>
{
    /// <summary>Room for an Entra ID object ID or a JWT <c>sub</c> (Phase 7).</summary>
    public const int OwnerIdMaxLength = 128;

    public void Configure(EntityTypeBuilder<MortgageApplication> builder)
    {
        builder.ToTable("MortgageApplications");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.OwnerId).IsRequired().HasMaxLength(OwnerIdMaxLength);
        builder.Property(a => a.AnnualIncome).HasPrecision(18, 2);
        builder.Property(a => a.MonthlyExpenses).HasPrecision(18, 2);
        builder.Property(a => a.DepositAmount).HasPrecision(18, 2);
        builder.Property(a => a.PropertyValue).HasPrecision(18, 2);
        builder.Property(a => a.TermYears);
        builder.Property(a => a.CreatedAt);

        // Every read filters on the owner.
        builder.HasIndex(a => a.OwnerId);
    }
}
