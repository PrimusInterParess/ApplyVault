using System.Text.RegularExpressions;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public sealed partial class ScrapeResultCaptureQualityService : IScrapeResultCaptureQualityService
{
    private const double LowConfidenceThreshold = 0.7;
    private const int MaxPositionSummaryLength = 280;
    private static readonly string[] VagueLocationTokens =
    [
        "multiple",
        "various",
        "not specified",
        "anywhere",
        "worldwide",
        "global",
        "across",
        "several locations",
        "many locations",
        "tbd",
        "to be determined"
    ];

    private static readonly string[] MisleadingLocationTokens =
    [
        "headquarters",
        "hq",
        "travel",
        "up to",
        "office locations",
        "global offices",
        "our offices"
    ];

    private static readonly string[] GenericTitleTokens =
    [
        "careers",
        "career",
        "jobs",
        "job",
        "job details",
        "job description",
        "apply now",
        "opportunity"
    ];

    public AssessedScrapeResult Assess(ScrapeResultDto request)
    {
        var normalizedTitle = NormalizeSingleLine(request.Title) ?? request.Title.Trim();
        var normalizedUrl = request.Url.Trim();
        var normalizedText = request.Text.Trim();
        var normalizedSourceHostname = NormalizeSingleLine(request.JobDetails.SourceHostname) ?? request.JobDetails.SourceHostname.Trim();
        var normalizedDetectedPageType = NormalizeSingleLine(request.JobDetails.DetectedPageType) ?? request.JobDetails.DetectedPageType.Trim();
        var normalizedJobTitle = NormalizeJobTitle(request.JobDetails.JobTitle, normalizedTitle);
        var normalizedCompanyName = NormalizeSingleLine(request.JobDetails.CompanyName);
        var normalizedLocation = NormalizeSingleLine(request.JobDetails.Location);
        var normalizedDescription = NormalizeMultiline(request.JobDetails.JobDescription);
        var normalizedSummary = NormalizePositionSummary(request.JobDetails.PositionSummary);
        var normalizedHiringManagerName = NormalizeSingleLine(request.JobDetails.HiringManagerName);
        var normalizedContacts = request.JobDetails.HiringManagerContacts
            .Select((contact) => new HiringManagerContactDto(
                NormalizeSingleLine(contact.Type) ?? contact.Type.Trim(),
                NormalizeSingleLine(contact.Value) ?? contact.Value.Trim(),
                NormalizeSingleLine(contact.Label)))
            .ToArray();

        var normalizedPayload = request with
        {
            Title = normalizedTitle,
            Url = normalizedUrl,
            Text = normalizedText,
            TextLength = normalizedText.Length,
            JobDetails = request.JobDetails with
            {
                SourceHostname = normalizedSourceHostname,
                DetectedPageType = normalizedDetectedPageType,
                JobTitle = normalizedJobTitle,
                CompanyName = normalizedCompanyName,
                Location = normalizedLocation,
                JobDescription = normalizedDescription,
                PositionSummary = normalizedSummary,
                HiringManagerName = normalizedHiringManagerName,
                HiringManagerContacts = normalizedContacts
            }
        };

        var jobTitleAssessment = AssessJobTitle(normalizedPayload.Title, normalizedPayload.JobDetails.JobTitle);
        var companyAssessment = AssessCompanyName(
            normalizedPayload.JobDetails.CompanyName,
            normalizedPayload.JobDetails.SourceHostname);
        var locationAssessment = AssessLocation(normalizedPayload.JobDetails.Location);
        var descriptionAssessment = AssessDescription(
            normalizedPayload.JobDetails.JobDescription,
            normalizedPayload.Text);
        var overallConfidence = Math.Round(new[]
        {
            jobTitleAssessment.Confidence,
            companyAssessment.Confidence,
            locationAssessment.Confidence,
            descriptionAssessment.Confidence
        }.Average(), 2);

        return new AssessedScrapeResult(
            normalizedPayload,
            new ScrapeResultCaptureQualityAssessment(
                overallConfidence,
                jobTitleAssessment,
                companyAssessment,
                locationAssessment,
                descriptionAssessment));
    }

    public static bool IsLowConfidence(double confidence)
    {
        return confidence < LowConfidenceThreshold;
    }

    private static ScrapeResultFieldAssessment AssessJobTitle(string payloadTitle, string? jobTitle)
    {
        if (string.IsNullOrWhiteSpace(jobTitle))
        {
            return new ScrapeResultFieldAssessment(0.32, "The role title was inferred from page context and should be reviewed.");
        }

        if (LooksGenericTitle(jobTitle))
        {
            return new ScrapeResultFieldAssessment(0.38, "The captured title looks generic rather than role-specific.");
        }

        if (string.Equals(jobTitle, payloadTitle, StringComparison.OrdinalIgnoreCase))
        {
            return new ScrapeResultFieldAssessment(0.81, null);
        }

        return new ScrapeResultFieldAssessment(0.9, null);
    }

    private static ScrapeResultFieldAssessment AssessCompanyName(string? companyName, string sourceHostname)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return new ScrapeResultFieldAssessment(0.25, "The employer name was not captured from the source page.");
        }

        var hostnameLabel = ExtractHostnameLabel(sourceHostname);

        if (!string.IsNullOrWhiteSpace(hostnameLabel) &&
            string.Equals(companyName, hostnameLabel, StringComparison.OrdinalIgnoreCase))
        {
            return new ScrapeResultFieldAssessment(0.46, "The company name appears to come from the source hostname and may need review.");
        }

        return new ScrapeResultFieldAssessment(0.88, null);
    }

    private static ScrapeResultFieldAssessment AssessLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return new ScrapeResultFieldAssessment(0.22, "No hiring location was detected.");
        }

        if (VagueLocationTokens.Any((token) => ContainsToken(location, token)))
        {
            return new ScrapeResultFieldAssessment(0.38, "The captured location is vague and should be confirmed.");
        }

        if (MisleadingLocationTokens.Any((token) => ContainsToken(location, token)))
        {
            return new ScrapeResultFieldAssessment(0.33, "The captured location may refer to office footprint, headquarters, or travel rather than the hiring location.");
        }

        if (LooksLikeOverlyLongLocation(location))
        {
            return new ScrapeResultFieldAssessment(0.42, "The captured location is unusually long and may contain multiple unrelated places or extra page text.");
        }

        if (ContainsToken(location, "remote") ||
            ContainsToken(location, "hybrid") ||
            ContainsToken(location, "on-site") ||
            ContainsToken(location, "onsite"))
        {
            if (ContainsToken(location, "remote") &&
                (ContainsToken(location, "worldwide") || ContainsToken(location, "global")))
            {
                return new ScrapeResultFieldAssessment(0.46, "The captured location is remote but still too broad or ambiguous.");
            }

            return new ScrapeResultFieldAssessment(0.84, null);
        }

        if (LooksLikeDelimitedMultiLocation(location))
        {
            return new ScrapeResultFieldAssessment(0.58, "The captured location may combine several possible locations and should be confirmed.");
        }

        return new ScrapeResultFieldAssessment(0.8, null);
    }

    private static ScrapeResultFieldAssessment AssessDescription(string? jobDescription, string fallbackText)
    {
        var description = string.IsNullOrWhiteSpace(jobDescription) ? fallbackText : jobDescription;
        var normalizedLength = description.Trim().Length;

        if (normalizedLength == 0)
        {
            return new ScrapeResultFieldAssessment(0.1, "No description text was captured.");
        }

        if (normalizedLength < 120)
        {
            return new ScrapeResultFieldAssessment(0.34, "The description is very short and may be incomplete.");
        }

        if (normalizedLength < 320)
        {
            return new ScrapeResultFieldAssessment(0.66, "The description is shorter than expected and may need cleanup.");
        }

        return new ScrapeResultFieldAssessment(0.9, null);
    }

    private static string? NormalizeJobTitle(string? jobTitle, string normalizedPayloadTitle)
    {
        var normalizedJobTitle = NormalizeSingleLine(jobTitle);

        if (!string.IsNullOrWhiteSpace(normalizedJobTitle))
        {
            return normalizedJobTitle;
        }

        return LooksGenericTitle(normalizedPayloadTitle) ? null : normalizedPayloadTitle;
    }

    private static string? NormalizeSingleLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = MultiWhitespaceRegex().Replace(value.Trim(), " ");
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizeMultiline(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        normalized = Regex.Replace(normalized, @"[ \t]+\n", "\n");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        normalized = normalized.Trim();

        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizePositionSummary(string? value)
    {
        var normalized = NormalizeSingleLine(value);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length <= MaxPositionSummaryLength)
        {
            return normalized;
        }

        var truncatedToSentence = TruncateAtSentenceBoundary(normalized, MaxPositionSummaryLength);
        return truncatedToSentence.Length <= MaxPositionSummaryLength
            ? truncatedToSentence
            : $"{normalized[..(MaxPositionSummaryLength - 1)].TrimEnd()}…";
    }

    private static bool LooksGenericTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return GenericTitleTokens.Any((token) => ContainsToken(value, token));
    }

    private static bool ContainsToken(string value, string token)
    {
        return value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static string TruncateAtSentenceBoundary(string value, int maxLength)
    {
        var candidate = value[..Math.Min(value.Length, maxLength)].TrimEnd();
        var sentenceBreak = candidate.LastIndexOfAny(['.', '!', '?']);

        if (sentenceBreak >= 40)
        {
            return candidate[..(sentenceBreak + 1)].TrimEnd();
        }

        return candidate;
    }

    private static bool LooksLikeOverlyLongLocation(string value)
    {
        return value.Length > 80;
    }

    private static bool LooksLikeDelimitedMultiLocation(string value)
    {
        var commaCount = value.Count((character) => character == ',');
        return commaCount >= 3 || value.Contains(" / ", StringComparison.Ordinal) || value.Contains(" | ", StringComparison.Ordinal);
    }

    private static string ExtractHostnameLabel(string sourceHostname)
    {
        var normalized = sourceHostname.Trim().Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase);
        var firstSegment = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? normalized;
        return NormalizeSingleLine(firstSegment.Replace('-', ' ').Replace('_', ' ')) ?? string.Empty;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();
}
