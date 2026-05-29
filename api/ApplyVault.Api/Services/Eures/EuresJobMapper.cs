using System.Text.RegularExpressions;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services.Shared;

namespace ApplyVault.Api.Services.Eures;

internal static class EuresJobMapper
{
    private static readonly Regex HrefRegex = new(
        "href=\"(?<url>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static EuresJobProfilePayload? ResolveSearchProfile(
        EuresSearchJobPayload job,
        string requestLanguage)
    {
        return ResolveProfile(job.JvProfiles, job.Translations, requestLanguage, job.PreferredLanguage);
    }

    public static EuresJobListingDto MapListing(EuresSearchJobPayload job, string requestLanguage)
    {
        var profile = ResolveSearchProfile(job, requestLanguage);
        var applicationUrl = ExtractFirstHref(profile?.ApplicationInstructions);
        var sourceUrl = applicationUrl ?? BuildEuresDetailUrl(job.Id);

        return new EuresJobListingDto(
            job.Id ?? string.Empty,
            profile?.Title ?? job.Title,
            profile?.Employer?.Name ?? job.Employer?.Name,
            FormatLocation(profile?.Locations, job.LocationMap),
            FormatPublicationDate(job.CreationDate),
            sourceUrl);
    }

    public static EuresJobDetailResponse MapDetail(EuresDetailResponsePayload detail, string requestLanguage)
    {
        var profile = ResolveProfile(detail.JvProfiles, translations: null, requestLanguage, detail.PreferredLanguage);
        var applicationUrl = ExtractFirstHref(profile?.ApplicationInstructions);
        var sourceUrl = applicationUrl ?? BuildEuresDetailUrl(detail.Id);

        return new EuresJobDetailResponse(
            detail.Id ?? string.Empty,
            profile?.Title,
            profile?.Employer?.Name,
            FormatLocation(profile?.Locations, locationMap: null),
            FormatPublicationDate(detail.CreationDate),
            sourceUrl,
            JobDescriptionHtmlSanitizer.Sanitize(profile?.Description),
            applicationUrl,
            profile?.PositionOfferingCode,
            FormatWorkHours(profile?.PositionScheduleCodes));
    }

    private static EuresJobProfilePayload? ResolveProfile(
        Dictionary<string, EuresJobProfilePayload>? profiles,
        Dictionary<string, EuresTranslationPayload>? translations,
        string requestLanguage,
        string? preferredLanguage)
    {
        if (profiles is not null)
        {
            if (TryGetProfile(profiles, requestLanguage, out var requestedProfile))
            {
                return requestedProfile;
            }

            if (!string.IsNullOrWhiteSpace(preferredLanguage)
                && TryGetProfile(profiles, preferredLanguage, out var preferredProfile))
            {
                return preferredProfile;
            }

            return profiles.Values.FirstOrDefault();
        }

        if (translations is not null)
        {
            if (TryGetTranslation(translations, requestLanguage, out var requestedTranslation))
            {
                return new EuresJobProfilePayload
                {
                    Title = requestedTranslation?.Title,
                    Description = requestedTranslation?.Description
                };
            }

            if (!string.IsNullOrWhiteSpace(preferredLanguage)
                && TryGetTranslation(translations, preferredLanguage, out var preferredTranslation))
            {
                return new EuresJobProfilePayload
                {
                    Title = preferredTranslation?.Title,
                    Description = preferredTranslation?.Description
                };
            }

            var firstTranslation = translations.Values.FirstOrDefault();
            return firstTranslation is null
                ? null
                : new EuresJobProfilePayload
                {
                    Title = firstTranslation.Title,
                    Description = firstTranslation.Description
                };
        }

        return null;
    }

    private static bool TryGetProfile(
        Dictionary<string, EuresJobProfilePayload> profiles,
        string language,
        out EuresJobProfilePayload? profile)
    {
        if (profiles.TryGetValue(language, out profile))
        {
            return true;
        }

        var match = profiles.FirstOrDefault(
            (entry) => entry.Key.Equals(language, StringComparison.OrdinalIgnoreCase));

        profile = string.IsNullOrWhiteSpace(match.Key) ? null : match.Value;
        return profile is not null;
    }

    private static bool TryGetTranslation(
        Dictionary<string, EuresTranslationPayload> translations,
        string language,
        out EuresTranslationPayload? translation)
    {
        if (translations.TryGetValue(language, out translation))
        {
            return true;
        }

        var match = translations.FirstOrDefault(
            (entry) => entry.Key.Equals(language, StringComparison.OrdinalIgnoreCase));

        translation = string.IsNullOrWhiteSpace(match.Key) ? null : match.Value;
        return translation is not null;
    }

    private static string? FormatLocation(
        IReadOnlyList<EuresLocationPayload>? locations,
        Dictionary<string, string[]>? locationMap)
    {
        if (locations is { Count: > 0 })
        {
            var formatted = locations
                .Select(FormatLocationEntry)
                .Where((value) => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (formatted.Length > 0)
            {
                return string.Join("; ", formatted);
            }
        }

        if (locationMap is null || locationMap.Count == 0)
        {
            return null;
        }

        return string.Join(
            "; ",
            locationMap.SelectMany((entry) => entry.Value.Select((region) => $"{entry.Key} {region}")));
    }

    private static string FormatLocationEntry(EuresLocationPayload location)
    {
        var parts = new[]
        {
            location.CityName,
            location.PostalCode,
            location.Region,
            location.CountryCode
        };

        return string.Join(", ", parts.Where((part) => !string.IsNullOrWhiteSpace(part)));
    }

    private static string? FormatPublicationDate(long? creationDate)
    {
        if (creationDate is null or <= 0)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(creationDate.Value).ToString("yyyy-MM-dd");
    }

    private static string? FormatWorkHours(IReadOnlyList<string>? scheduleCodes)
    {
        if (scheduleCodes is null || scheduleCodes.Count == 0)
        {
            return null;
        }

        return string.Join(", ", scheduleCodes);
    }

    private static string? ExtractFirstHref(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var match = HrefRegex.Match(value);
            if (match.Success)
            {
                return match.Groups["url"].Value;
            }
        }

        return null;
    }

    private static string? BuildEuresDetailUrl(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return $"https://europa.eu/eures/portal/jv/detail/jv?id={Uri.EscapeDataString(id.Trim())}";
    }
}
