using System.Net;
using System.Text.RegularExpressions;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Jobnet;

internal static class JobnetScrapeResultMapper
{
    private static readonly Regex HtmlTagPattern = new(
        "<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ScrapeResultDto MapToScrapeResult(JobnetJobDetailResponse detail)
    {
        var title = NormalizeSingleLine(detail.Title) ?? "Untitled listing";
        var url = JobnetJobMapper.ResolveSourceUrl(detail.Id, detail.ApplicationUrl ?? detail.SourceUrl);
        var descriptionText = NormalizeSingleLine(detail.Description) ?? string.Empty;
        var text = descriptionText.Length > 0
            ? descriptionText
            : BuildFallbackText(title, detail.Employer, detail.Location);

        return new ScrapeResultDto(
            Title: title,
            Url: url,
            Text: text,
            TextLength: text.Length,
            ExtractedAt: DateTimeOffset.UtcNow.ToString("O"),
            JobDetails: new JobDetailsDto(
                SourceHostname: "jobnet.dk",
                DetectedPageType: "jobnet-job",
                JobTitle: title,
                CompanyName: NormalizeSingleLine(detail.Employer),
                Location: NormalizeSingleLine(detail.Location),
                JobDescription: descriptionText.Length > 0 ? descriptionText : text,
                PositionSummary: null,
                HiringManagerName: null,
                HiringManagerContacts: []));
    }

    public static string ResolveCanonicalUrl(JobnetJobDetailResponse detail)
    {
        var applicationUrl = NormalizeSingleLine(detail.ApplicationUrl);
        if (!string.IsNullOrWhiteSpace(applicationUrl))
        {
            return applicationUrl;
        }

        return JobnetJobMapper.ResolveSourceUrl(detail.Id, detail.SourceUrl);
    }

    private static string BuildFallbackText(string title, string? employer, string? location)
    {
        var parts = new List<string> { title };

        var normalizedEmployer = NormalizeSingleLine(employer);
        if (!string.IsNullOrWhiteSpace(normalizedEmployer))
        {
            parts.Add($"Employer: {normalizedEmployer}");
        }

        var normalizedLocation = NormalizeSingleLine(location);
        if (!string.IsNullOrWhiteSpace(normalizedLocation))
        {
            parts.Add($"Location: {normalizedLocation}");
        }

        return string.Join("\n", parts);
    }

    private static string? NormalizeSingleLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var withoutTags = HtmlTagPattern.Replace(value, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded.Trim(), @"\s+", " ");
    }
}
