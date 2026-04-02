using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class ScrapeResultEnrichmentService(
    IScrapeResultAiClient aiClient,
    IOptions<ScrapeResultEnrichmentOptions> enrichmentOptions,
    ILogger<ScrapeResultEnrichmentService> logger) : IScrapeResultEnrichmentService
{
    public async Task<ScrapeResultDto> EnrichIfNeededAsync(
        ScrapeResultDto request,
        CancellationToken cancellationToken = default)
    {
        if (!enrichmentOptions.Value.Enabled || HasJobDescription(request))
        {
            return request;
        }

        try
        {
            var aiResult = await aiClient.EnrichAsync(request, cancellationToken);
            return Merge(request, aiResult);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Google AI enrichment failed for scraped result {Url}.",
                request.Url);

            if (enrichmentOptions.Value.FailOnAiError)
            {
                throw;
            }

            return request;
        }
    }

    private static bool HasJobDescription(ScrapeResultDto request)
    {
        return !string.IsNullOrWhiteSpace(request.JobDetails.JobDescription);
    }

    private static ScrapeResultDto Merge(ScrapeResultDto original, ScrapeResultDto enriched)
    {
        var originalContacts = original.JobDetails.HiringManagerContacts;
        var enrichedContacts = enriched.JobDetails.HiringManagerContacts;

        return original with
        {
            JobDetails = original.JobDetails with
            {
                SourceHostname = CoalesceRequired(original.JobDetails.SourceHostname, enriched.JobDetails.SourceHostname),
                DetectedPageType = CoalesceRequired(original.JobDetails.DetectedPageType, enriched.JobDetails.DetectedPageType),
                JobTitle = CoalesceOptional(original.JobDetails.JobTitle, enriched.JobDetails.JobTitle),
                CompanyName = CoalesceOptional(original.JobDetails.CompanyName, enriched.JobDetails.CompanyName),
                Location = CoalesceOptional(original.JobDetails.Location, enriched.JobDetails.Location),
                JobDescription = CoalesceOptional(original.JobDetails.JobDescription, enriched.JobDetails.JobDescription),
                PositionSummary = CoalesceOptional(original.JobDetails.PositionSummary, enriched.JobDetails.PositionSummary),
                HiringManagerName = CoalesceOptional(original.JobDetails.HiringManagerName, enriched.JobDetails.HiringManagerName),
                HiringManagerContacts = originalContacts.Count > 0 ? originalContacts : enrichedContacts
            }
        };
    }

    private static string CoalesceRequired(string current, string fallback)
    {
        return string.IsNullOrWhiteSpace(current) ? fallback : current;
    }

    private static string? CoalesceOptional(string? current, string? fallback)
    {
        return string.IsNullOrWhiteSpace(current) ? Normalize(fallback) : Normalize(current);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
