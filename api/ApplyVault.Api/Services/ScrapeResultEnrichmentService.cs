using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class ScrapeResultEnrichmentService(
    IScrapeResultAiClient aiClient,
    IOptions<ScrapeResultEnrichmentOptions> enrichmentOptions,
    ILogger<ScrapeResultEnrichmentService> logger) : IScrapeResultEnrichmentService
{
    private const double LowConfidenceThreshold = 0.7;

    public async Task<ScrapeResultDto> EnrichLowConfidenceFieldsAsync(
        AssessedScrapeResult assessment,
        CancellationToken cancellationToken = default)
    {
        if (!enrichmentOptions.Value.Enabled || !NeedsEnrichment(assessment.CaptureQuality))
        {
            return assessment.Payload;
        }

        try
        {
            var repairGuidance = BuildRepairGuidance(assessment);
            var aiResult = await aiClient.EnrichAsync(assessment.Payload, repairGuidance, cancellationToken);
            return Merge(assessment, aiResult);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Google AI enrichment failed for scraped result {Url}.",
                assessment.Payload.Url);

            if (enrichmentOptions.Value.FailOnAiError)
            {
                throw;
            }

            return assessment.Payload;
        }
    }

    private static bool NeedsEnrichment(ScrapeResultCaptureQualityAssessment assessment)
    {
        return IsLowConfidence(assessment.JobTitle) ||
            IsLowConfidence(assessment.CompanyName) ||
            IsLowConfidence(assessment.Location) ||
            IsLowConfidence(assessment.JobDescription);
    }

    private static ScrapeResultDto Merge(AssessedScrapeResult assessment, ScrapeResultDto enriched)
    {
        var original = assessment.Payload;
        var quality = assessment.CaptureQuality;
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
                JobTitle = SelectOptional(quality.JobTitle, original.JobDetails.JobTitle, enriched.JobDetails.JobTitle),
                CompanyName = SelectOptional(quality.CompanyName, original.JobDetails.CompanyName, enriched.JobDetails.CompanyName),
                Location = SelectOptional(quality.Location, original.JobDetails.Location, enriched.JobDetails.Location),
                JobDescription = SelectOptional(quality.JobDescription, original.JobDetails.JobDescription, enriched.JobDetails.JobDescription),
                PositionSummary = CoalesceOptional(enriched.JobDetails.PositionSummary, original.JobDetails.PositionSummary),
                HiringManagerName = CoalesceOptional(enriched.JobDetails.HiringManagerName, original.JobDetails.HiringManagerName),
                HiringManagerContacts = SelectContacts(
                    quality.JobTitle,
                    quality.CompanyName,
                    quality.Location,
                    quality.JobDescription,
                    enrichedContacts,
                    originalContacts)
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

    private static string? SelectOptional(
        ScrapeResultFieldAssessment assessment,
        string? originalValue,
        string? enrichedValue)
    {
        if (!IsLowConfidence(assessment))
        {
            return Normalize(originalValue);
        }

        return CoalesceOptional(enrichedValue, originalValue);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyList<HiringManagerContactDto> SelectContacts(
        ScrapeResultFieldAssessment jobTitleAssessment,
        ScrapeResultFieldAssessment companyAssessment,
        ScrapeResultFieldAssessment locationAssessment,
        ScrapeResultFieldAssessment descriptionAssessment,
        IReadOnlyList<HiringManagerContactDto> primary,
        IReadOnlyList<HiringManagerContactDto> fallback)
    {
        if (!IsLowConfidence(descriptionAssessment) &&
            !IsLowConfidence(jobTitleAssessment) &&
            !IsLowConfidence(companyAssessment) &&
            !IsLowConfidence(locationAssessment))
        {
            return fallback;
        }

        return primary.Count > 0 ? primary : fallback;
    }

    private static bool IsLowConfidence(ScrapeResultFieldAssessment assessment)
    {
        return assessment.Confidence < LowConfidenceThreshold;
    }

    private static string BuildRepairGuidance(AssessedScrapeResult assessment)
    {
        var guidance = new List<string>
        {
            "- Keep high-confidence fields unchanged unless the payload contains stronger explicit evidence.",
            "- Focus corrections on fields marked low-confidence below.",
            "- Prefer explicit labeled metadata over inferred text.",
            "- Do not use company headquarters, office directories, or general company footprint as the hiring location unless the role clearly uses that location."
        };

        AppendFieldGuidance(guidance, "jobTitle", assessment.CaptureQuality.JobTitle);
        AppendFieldGuidance(guidance, "companyName", assessment.CaptureQuality.CompanyName);
        AppendFieldGuidance(guidance, "location", assessment.CaptureQuality.Location);
        AppendFieldGuidance(guidance, "jobDescription", assessment.CaptureQuality.JobDescription);

        if (IsLowConfidence(assessment.CaptureQuality.Location))
        {
            guidance.Add("- For location, look first for explicit labels such as location, workplace, office, based in, remote, hybrid, or on-site.");
            guidance.Add("- Distinguish work model from geography. If both are supported, return a concise combined value such as 'Remote - United States' or 'Hybrid - London, UK'.");
            guidance.Add("- If the page clearly supports multiple valid hiring locations, summarize them concisely. Otherwise prefer the most specific explicitly supported hiring location.");
            guidance.Add("- Ignore travel requirements, recruiter location, company headquarters, and unrelated office lists when determining location.");
        }

        return string.Join('\n', guidance);
    }

    private static void AppendFieldGuidance(
        List<string> guidance,
        string fieldName,
        ScrapeResultFieldAssessment assessment)
    {
        if (!IsLowConfidence(assessment))
        {
            return;
        }

        var reason = string.IsNullOrWhiteSpace(assessment.ReviewReason)
            ? "low confidence"
            : assessment.ReviewReason;
        guidance.Add($"- {fieldName} is low-confidence: {reason}");
    }
}
