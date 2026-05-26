using System.Net;
using System.Text.RegularExpressions;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Eures;

internal static class EuresScrapeResultMapper
{
    private static readonly Regex HtmlTagPattern = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ScrapeResultDto MapToScrapeResult(EuresJobDetailResponse detail)
    {
        var title = NormalizeSingleLine(detail.Title) ?? "Untitled listing";
        var url = ResolveCanonicalUrl(detail);
        var descriptionText = StripHtml(detail.Description);
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
                SourceHostname: "europa.eu",
                DetectedPageType: "eures-job",
                JobTitle: title,
                CompanyName: NormalizeSingleLine(detail.Employer),
                Location: NormalizeSingleLine(detail.Location),
                JobDescription: descriptionText.Length > 0 ? descriptionText : text,
                PositionSummary: null,
                HiringManagerName: null,
                HiringManagerContacts: []));
    }

    public static string ResolveCanonicalUrl(EuresJobDetailResponse detail)
    {
        var applicationUrl = NormalizeSingleLine(detail.ApplicationUrl);
        if (!string.IsNullOrWhiteSpace(applicationUrl))
        {
            return applicationUrl;
        }

        var sourceUrl = NormalizeSingleLine(detail.SourceUrl);
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            return sourceUrl;
        }

        return $"https://europa.eu/eures/portal/jv/detail/jv?id={Uri.EscapeDataString(detail.Id.Trim())}";
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

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutTags = HtmlTagPattern.Replace(html, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    private static string? NormalizeSingleLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Regex.Replace(value.Trim(), @"\s+", " ");
    }
}
