using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApplyVault.Api.Services.JobRequirements;

internal static class JobRequirementsPersistence
{
    public static void Configure(EntityTypeBuilder<ScrapeResultEntity> entity)
    {
        entity.Property(job => job.RequirementsRevision).HasDefaultValue(0L).IsConcurrencyToken();
        entity.Property(job => job.RequirementsSourceHash).HasMaxLength(64);
        entity.Property(job => job.RequirementsStatus).HasConversion<int>()
            .HasDefaultValue(JobRequirementsStatus.Uninitialized);
        entity.Property(job => job.RequirementsJson).HasColumnType("nvarchar(max)");
        entity.Property(job => job.RequirementsAttempts).HasDefaultValue(0);
        entity.Property(job => job.RequirementsNextAttemptUtc);
        entity.Property(job => job.RequirementsLeaseId);
        entity.Property(job => job.RequirementsLeaseExpiresUtc);
        entity.HasIndex(job => new { job.IsDeleted, job.RequirementsStatus, job.RequirementsNextAttemptUtc });
    }
}
