using System.Net;
using System.Text.RegularExpressions;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Jobnet;

internal static class JobnetJobMapper
{
    private static readonly Regex HtmlTagPattern = new(
        "<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const string WorkInDenmarkClassification = "WorkInDenmark";

    public static bool HasWorkInDenmarkClassification(IEnumerable<string>? classifications)
    {
        return classifications?.Any(
            (classification) => classification.Equals(
                WorkInDenmarkClassification,
                StringComparison.OrdinalIgnoreCase)) == true;
    }

    public static JobnetJobListingDto MapListing(
        JobnetSearchJobPayload job,
        bool workInDenmark)
    {
        var id = job.JobAdId?.Trim() ?? string.Empty;

        return new JobnetJobListingDto(
            id,
            NormalizeSingleLine(job.Title),
            NormalizeSingleLine(job.HiringOrgName),
            FormatLocation(job.WorkPlaceAddress, job.Country),
            FormatPublicationDate(job.PublicationDate),
            ResolveSourceUrl(id, job.JobAdUrl),
            workInDenmark);
    }

    public static JobnetJobDetailResponse MapDetail(
        string id,
        JobnetDetailResponsePayload detail)
    {
        var workInDenmark = HasWorkInDenmarkClassification(detail.Job?.Classifications);
        var employer = NormalizeSingleLine(detail.Employer?.Name ?? detail.Job?.HiringOrgName);
        var location = FormatLocation(
            detail.Job?.WorkPlaceAddress,
            detail.Employer?.Address?.CountryName,
            detail.Employer?.Address);

        return new JobnetJobDetailResponse(
            id,
            NormalizeSingleLine(detail.Title),
            employer,
            location,
            FormatPublicationDate(detail.PublicationDateTime),
            ResolveSourceUrl(id, detail.Application?.UrlText),
            StripHtml(detail.Body),
            ResolveApplicationUrl(detail.Application),
            detail.Job?.Occupation,
            FormatWorkHours(detail.Job?.WorkHourPartTime),
            workInDenmark);
    }

    public static JobnetJobDetailResponse MapDetailFromSearch(
        string id,
        JobnetSearchJobPayload job)
    {
        var location = FormatLocation(
            job.WorkPlaceAddress,
            job.Country,
            postalCode: job.PostalDistrictName ?? job.PostalCode?.ToString());

        return new JobnetJobDetailResponse(
            id,
            NormalizeSingleLine(job.Title),
            NormalizeSingleLine(job.HiringOrgName),
            location,
            FormatPublicationDate(job.PublicationDate),
            ResolveSourceUrl(id, job.JobAdUrl),
            StripHtml(job.Description),
            ResolveApplicationUrlFromSearch(job),
            job.Occupation,
            FormatWorkHours(job.WorkHourPartTime),
            WorkInDenmark: JobnetJobIdentifiers.IsEuresImported(id));
    }

    public static string ResolveSourceUrl(string id, string? jobAdUrl)
    {
        var normalizedUrl = NormalizeSingleLine(jobAdUrl);
        if (!string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return normalizedUrl;
        }

        return BuildJobnetDetailUrl(id);
    }

    private static string? ResolveApplicationUrlFromSearch(JobnetSearchJobPayload job)
    {
        return ResolveSourceUrl(job.JobAdId ?? string.Empty, job.JobAdUrl);
    }

    private static string? ResolveApplicationUrl(JobnetDetailApplicationPayload? application)
    {
        var urlText = NormalizeSingleLine(application?.UrlText);
        if (!string.IsNullOrWhiteSpace(urlText))
        {
            return urlText;
        }

        return NormalizeSingleLine(application?.EmailAddress) is { Length: > 0 } email
            ? $"mailto:{email}"
            : null;
    }

    private static string BuildJobnetDetailUrl(string id)
    {
        return $"https://jobnet.dk/find-job?jobAdId={Uri.EscapeDataString(id.Trim())}";
    }

    private static string? FormatLocation(
        string? workPlaceAddress,
        string? country,
        JobnetDetailAddressPayload? address = null,
        string? postalCode = null)
    {
        if (!string.IsNullOrWhiteSpace(workPlaceAddress))
        {
            var normalizedAddress = NormalizeSingleLine(workPlaceAddress);
            if (!string.IsNullOrWhiteSpace(postalCode) && normalizedAddress is not null
                && !normalizedAddress.Contains(postalCode, StringComparison.OrdinalIgnoreCase))
            {
                return $"{normalizedAddress}, {postalCode.Trim()}";
            }

            return normalizedAddress;
        }

        if (address is not null)
        {
            var parts = new[]
            {
                address.City,
                address.PostalCode,
                address.Municipality,
                address.CountryName
            };

            var formatted = string.Join(
                ", ",
                parts.Where((part) => !string.IsNullOrWhiteSpace(part)).Select((part) => part!.Trim()));

            if (formatted.Length > 0)
            {
                return formatted;
            }
        }

        return NormalizeSingleLine(country);
    }

    private static string? FormatPublicationDate(string? publicationDate)
    {
        if (string.IsNullOrWhiteSpace(publicationDate))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(publicationDate, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd");
        }

        return publicationDate.Trim();
    }

    private static string? FormatWorkHours(bool? workHourPartTime)
    {
        return workHourPartTime switch
        {
            true => "Part-time",
            false => "Full-time",
            _ => null
        };
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
