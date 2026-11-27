using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services.JobRequirements;

internal sealed record JobRequirementsClaim(
    Guid JobId, Guid UserId, long Revision, string SourceHash, string Description, Guid LeaseId, int Attempts);

internal sealed class JobRequirementsWorkStore(ApplyVaultDbContext db)
{
    public async Task BackfillAsync(int batchSize, CancellationToken cancellationToken)
    {
        var jobs = await db.ScrapeResults.AsNoTracking()
            .Where(job => !job.IsDeleted && job.RequirementsStatus == JobRequirementsStatus.Uninitialized)
            .OrderBy(job => job.SavedAt).ThenBy(job => job.Id).Take(batchSize).ToArrayAsync(cancellationToken);
        foreach (var job in jobs)
        {
            await SynchronizeSourceAsync(job, cancellationToken);
        }
    }

    public async Task<JobRequirementsClaim?> ClaimNextAsync(DateTime nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var job = await Eligible(nowUtc).AsNoTracking()
            .OrderBy(job => job.RequirementsNextAttemptUtc).ThenBy(job => job.SavedAt).ThenBy(job => job.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            return null;
        }

        var sourceHash = job.RequirementsSourceHash;
        var description = JobRequirementsSource.Description(job);
        if (string.IsNullOrWhiteSpace(sourceHash) || job.RequirementsRevision <= 0 ||
            !string.Equals(sourceHash, JobRequirementsSource.Fingerprint(description), StringComparison.Ordinal))
        {
            await SynchronizeSourceAsync(job, cancellationToken);
            return null;
        }

        var leaseId = Guid.NewGuid();
        var expiresUtc = nowUtc + leaseDuration;
        var updated = await Eligible(nowUtc).Where(current => current.Id == job.Id && current.UserId == job.UserId &&
                current.RequirementsRevision == job.RequirementsRevision)
            .ExecuteUpdateAsync(update => update
                .SetProperty(current => current.RequirementsStatus, JobRequirementsStatus.Processing)
                .SetProperty(current => current.RequirementsLeaseId, leaseId)
                .SetProperty(current => current.RequirementsLeaseExpiresUtc, expiresUtc), cancellationToken);
        return updated == 1
            ? new JobRequirementsClaim(job.Id, job.UserId, job.RequirementsRevision,
                sourceHash, description, leaseId, job.RequirementsAttempts)
            : null;
    }

    public Task CompleteAsync(JobRequirementsClaim claim, string json, DateTime nowUtc, CancellationToken cancellationToken) =>
        OwnedClaim(claim, nowUtc).ExecuteUpdateAsync(update => update
            .SetProperty(job => job.RequirementsStatus, JobRequirementsStatus.Ready)
            .SetProperty(job => job.RequirementsJson, json)
            .SetProperty(job => job.RequirementsLeaseId, (Guid?)null)
            .SetProperty(job => job.RequirementsLeaseExpiresUtc, (DateTime?)null)
            .SetProperty(job => job.RequirementsNextAttemptUtc, (DateTime?)null), cancellationToken);

    public Task FailAsync(JobRequirementsClaim claim, DateTime nowUtc, JobRequirementsOptions options, CancellationToken cancellationToken)
    {
        var attempts = claim.Attempts + 1;
        var status = attempts >= options.MaxAttempts ? JobRequirementsStatus.Failed : JobRequirementsStatus.Pending;
        var retryUtc = status == JobRequirementsStatus.Pending ? nowUtc.AddSeconds(options.RetryDelaySeconds) : (DateTime?)null;
        return OwnedClaim(claim, nowUtc).ExecuteUpdateAsync(update => update
            .SetProperty(job => job.RequirementsStatus, status)
            .SetProperty(job => job.RequirementsAttempts, attempts)
            .SetProperty(job => job.RequirementsNextAttemptUtc, retryUtc)
            .SetProperty(job => job.RequirementsLeaseId, (Guid?)null)
            .SetProperty(job => job.RequirementsLeaseExpiresUtc, (DateTime?)null), cancellationToken);
    }

    private Task SynchronizeSourceAsync(ScrapeResultEntity job, CancellationToken cancellationToken)
    {
        var previousRevision = job.RequirementsRevision;
        var previousHash = job.RequirementsSourceHash;
        var previousStatus = job.RequirementsStatus;
        JobRequirementsSource.Synchronize(job);
        return db.ScrapeResults.Where(current => current.Id == job.Id && current.UserId == job.UserId &&
                !current.IsDeleted && current.RequirementsRevision == previousRevision &&
                current.RequirementsSourceHash == previousHash && current.RequirementsStatus == previousStatus)
            .ExecuteUpdateAsync(update => update
                .SetProperty(current => current.RequirementsRevision, job.RequirementsRevision)
                .SetProperty(current => current.RequirementsSourceHash, job.RequirementsSourceHash)
                .SetProperty(current => current.RequirementsStatus, job.RequirementsStatus)
                .SetProperty(current => current.RequirementsJson, (string?)null)
                .SetProperty(current => current.RequirementsAttempts, 0)
                .SetProperty(current => current.RequirementsNextAttemptUtc, (DateTime?)null)
                .SetProperty(current => current.RequirementsLeaseId, (Guid?)null)
                .SetProperty(current => current.RequirementsLeaseExpiresUtc, (DateTime?)null), cancellationToken);
    }

    private IQueryable<ScrapeResultEntity> Eligible(DateTime nowUtc) => db.ScrapeResults.Where(job => !job.IsDeleted &&
        ((job.RequirementsStatus == JobRequirementsStatus.Pending &&
          (job.RequirementsNextAttemptUtc == null || job.RequirementsNextAttemptUtc <= nowUtc)) ||
         (job.RequirementsStatus == JobRequirementsStatus.Processing && job.RequirementsLeaseExpiresUtc <= nowUtc)));

    private IQueryable<ScrapeResultEntity> OwnedClaim(JobRequirementsClaim claim, DateTime nowUtc) => db.ScrapeResults.Where(job =>
        job.Id == claim.JobId && job.UserId == claim.UserId && !job.IsDeleted &&
        job.RequirementsRevision == claim.Revision && job.RequirementsSourceHash == claim.SourceHash &&
        job.RequirementsStatus == JobRequirementsStatus.Processing && job.RequirementsLeaseId == claim.LeaseId &&
        job.RequirementsLeaseExpiresUtc > nowUtc);
}
