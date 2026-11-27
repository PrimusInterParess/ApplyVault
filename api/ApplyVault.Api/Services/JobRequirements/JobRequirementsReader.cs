using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services.JobRequirements;

internal sealed class JobRequirementsReader(ApplyVaultDbContext db) : IJobRequirementsReader
{
    public async Task<JobRequirementsResult?> GetCurrentAsync(Guid jobId, Guid userId, CancellationToken cancellationToken = default)
    {
        var job = await db.ScrapeResults.AsNoTracking().SingleOrDefaultAsync(
            job => job.Id == jobId && job.UserId == userId && !job.IsDeleted, cancellationToken);
        if (job is null)
        {
            return null;
        }

        var description = JobRequirementsSource.Description(job);
        var status = job.RequirementsStatus;
        if (status == JobRequirementsStatus.Uninitialized ||
            !string.Equals(job.RequirementsSourceHash, JobRequirementsSource.Fingerprint(description), StringComparison.Ordinal))
        {
            status = JobRequirementsSource.HasUsableDescription(description) ? JobRequirementsStatus.Pending : JobRequirementsStatus.DescriptionNeeded;
        }

        var document = status == JobRequirementsStatus.Ready && job.RequirementsJson is not null
            ? JobRequirementsJson.Parse(job.RequirementsJson, description)
            : null;
        return new JobRequirementsResult(status, document);
    }
}
