using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.JobRequirements;

internal sealed class JobRequirementsProcessor(
    JobRequirementsWorkStore store,
    IJobRequirementsAiClient provider,
    IOptions<JobRequirementsOptions> options,
    TimeProvider clock,
    ILogger<JobRequirementsProcessor> logger)
{
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        await store.BackfillAsync(settings.BatchSize, cancellationToken);
        var processed = 0;
        for (var index = 0; index < settings.BatchSize; index++)
        {
            var claim = await store.ClaimNextAsync(clock.GetUtcNow().UtcDateTime, settings.LeaseDuration, cancellationToken);
            if (claim is null)
            {
                break;
            }

            await ExtractAsync(claim, settings, cancellationToken);
            processed++;
        }

        return processed;
    }

    private async Task ExtractAsync(JobRequirementsClaim claim, JobRequirementsOptions settings, CancellationToken cancellationToken)
    {
        try
        {
            if (claim.Description.Length > JobRequirementsSource.MaxDescriptionCharacters)
            {
                throw new InvalidDataException("Job description exceeds extraction limit.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            var json = await provider.ExtractAsync(claim.Description, timeout.Token);
            var document = JobRequirementsJson.Parse(json, claim.Description);
            await store.CompleteAsync(claim, JobRequirementsJson.Serialize(document), clock.GetUtcNow().UtcDateTime, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The durable lease expires without spending a failure attempt.
            throw;
        }
        catch (Exception exception)
        {
            // Provider exception messages may contain credentials or posting content.
            logger.LogWarning("Job requirements extraction failed for job {JobId}, attempt {Attempt}, error type {ErrorType}.",
                claim.JobId, claim.Attempts + 1, exception.GetType().Name);
            await store.FailAsync(claim, clock.GetUtcNow().UtcDateTime, settings, cancellationToken);
        }
    }
}
