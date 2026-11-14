using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MortgageApplications.Core.Affordability;
using MortgageApplications.Core.Applications;

namespace MortgageApplications.Infrastructure.Persistence;

/// <summary>
/// The result is one JSON column (P3-D3, fallback b), null while Pending. Enums are stored as strings.
/// </summary>
internal sealed class AffordabilityAssessmentConfiguration : IEntityTypeConfiguration<AffordabilityAssessment>
{
    public void Configure(EntityTypeBuilder<AffordabilityAssessment> builder)
    {
        builder.ToTable("AffordabilityAssessments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.ApplicationId);
        builder.Property(a => a.CreatedAt);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(16);

        builder.HasOne<MortgageApplication>()
            .WithMany()
            .HasForeignKey(a => a.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.ApplicationId);

        // P3-D3 fallback (b): one JSON column, null while Pending. Why not a complex type: AffordabilityResultJson.
        builder.Property(a => a.Result)
            .HasConversion(AffordabilityResultJson.Converter, AffordabilityResultJson.Comparer)
            .HasColumnName("Result");
    }
}
