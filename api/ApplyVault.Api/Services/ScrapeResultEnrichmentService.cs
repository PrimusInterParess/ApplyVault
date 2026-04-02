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

        return enriched with
        {
            Title = enriched.Title,
            Url = original.Url,
            Text = original.Text,
            TextLength = original.TextLength,
            ExtractedAt = original.ExtractedAt,
            JobDetails = enriched.JobDetails with
            {
                SourceHostname = CoalesceRequired(original.JobDetails.SourceHostname, enriched.JobDetails.SourceHostname),
                DetectedPageType = CoalesceRequired(original.JobDetails.DetectedPageType, enriched.JobDetails.DetectedPageType),
                JobTitle = CoalesceOptional(enriched.JobDetails.JobTitle, original.JobDetails.JobTitle),
                CompanyName = CoalesceOptional(enriched.JobDetails.CompanyName, original.JobDetails.CompanyName),
                Location = CoalesceOptional(enriched.JobDetails.Location, original.JobDetails.Location),
                JobDescription = CoalesceOptional(enriched.JobDetails.JobDescription, original.JobDetails.JobDescription),
                PositionSummary = CoalesceOptional(enriched.JobDetails.PositionSummary, original.JobDetails.PositionSummary),
                HiringManagerName = CoalesceOptional(enriched.JobDetails.HiringManagerName, original.JobDetails.HiringManagerName),
                HiringManagerContacts = SelectContacts(enrichedContacts, originalContacts)
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

    private static IReadOnlyList<HiringManagerContactDto> SelectContacts(
        IReadOnlyList<HiringManagerContactDto> primary,
        IReadOnlyList<HiringManagerContactDto> fallback)
    {
        return primary.Count > 0 ? primary : fallback;
    }
}
