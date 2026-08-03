using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Eures;

public sealed class EuresJobSearchRequestNormalizer(IOptions<EuresIntegrationOptions> options)
    : IEuresJobSearchRequestNormalizer
{
    public bool TryNormalizeSearchRequest(
        EuresJobSearchRequest request,
        out EuresJobSearchRequest normalizedRequest,
        out string validationMessage)
    {
        var normalizedKeywords = request.ResolveKeywords()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedKeywords.Length == 0)
        {
            normalizedRequest = request;
            validationMessage = "At least one keyword is required.";
            return false;
        }

        if (!TryNormalizeSortSearch(
                request.SortSearch,
                multipleKeywords: normalizedKeywords.Length > 1,
                out var normalizedSortSearch,
                out validationMessage))
        {
            normalizedRequest = request;
            return false;
        }

        if (!TryNormalizePublicationPeriod(
                request.PublicationPeriod,
                out var normalizedPublicationPeriod,
                out validationMessage))
        {
            normalizedRequest = request;
            return false;
        }

        if (!TryNormalizePositionScheduleCodes(
                request.PositionScheduleCodes,
                out var normalizedScheduleCodes,
                out validationMessage))
        {
            normalizedRequest = request;
            return false;
        }

        var integrationOptions = options.Value;
        var cappedResults = Math.Clamp(
            request.ResultsPerPage,
            1,
            Math.Max(1, integrationOptions.MaxResultsPerPage));

        normalizedRequest = new EuresJobSearchRequest
        {
            Keywords = normalizedKeywords.ToList(),
            Keyword = normalizedKeywords.Length == 1 ? normalizedKeywords[0] : null,
            LocationCode = string.IsNullOrWhiteSpace(request.LocationCode)
                ? integrationOptions.DefaultLocationCode
                : request.LocationCode.Trim(),
            Page = Math.Max(1, request.Page),
            ResultsPerPage = cappedResults,
            RequestLanguage = NormalizeRequestLanguage(request.RequestLanguage),
            SortSearch = normalizedSortSearch,
            PublicationPeriod = normalizedPublicationPeriod,
            PositionScheduleCodes = normalizedScheduleCodes
        };

        validationMessage = string.Empty;
        return true;
    }

    public string NormalizeRequestLanguage(string? requestLanguage)
    {
        return string.IsNullOrWhiteSpace(requestLanguage) ? "en" : requestLanguage.Trim();
    }

    private static bool TryNormalizeSortSearch(
        string? sortSearch,
        bool multipleKeywords,
        out string normalizedSortSearch,
        out string validationMessage)
    {
        if (multipleKeywords)
        {
            normalizedSortSearch = "BEST_MATCH";
            validationMessage = string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(sortSearch))
        {
            normalizedSortSearch = "MOST_RECENT";
            validationMessage = string.Empty;
            return true;
        }

        var trimmed = sortSearch.Trim();
        if (!EuresSearchFilterCodes.SortSearch.Contains(trimmed))
        {
            normalizedSortSearch = string.Empty;
            validationMessage = "sortSearch must be MOST_RECENT or BEST_MATCH.";
            return false;
        }

        normalizedSortSearch = EuresSearchFilterCodes.CanonicalSortSearch(trimmed);
        validationMessage = string.Empty;
        return true;
    }

    private static bool TryNormalizePublicationPeriod(
        string? publicationPeriod,
        out string? normalizedPublicationPeriod,
        out string validationMessage)
    {
        if (string.IsNullOrWhiteSpace(publicationPeriod))
        {
            normalizedPublicationPeriod = null;
            validationMessage = string.Empty;
            return true;
        }

        var trimmed = publicationPeriod.Trim();
        if (!EuresSearchFilterCodes.PublicationPeriods.Contains(trimmed))
        {
            normalizedPublicationPeriod = null;
            validationMessage = "publicationPeriod must be LAST_WEEK or LAST_MONTH.";
            return false;
        }

        normalizedPublicationPeriod = EuresSearchFilterCodes.CanonicalPublicationPeriod(trimmed);
        validationMessage = string.Empty;
        return true;
    }

    private static bool TryNormalizePositionScheduleCodes(
        IReadOnlyList<string>? positionScheduleCodes,
        out List<string>? normalizedScheduleCodes,
        out string validationMessage)
    {
        if (positionScheduleCodes is null || positionScheduleCodes.Count == 0)
        {
            normalizedScheduleCodes = null;
            validationMessage = string.Empty;
            return true;
        }

        var canonical = new List<string>();
        foreach (var code in positionScheduleCodes)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var trimmed = code.Trim();
            if (!EuresSearchFilterCodes.PositionScheduleCodes.Contains(trimmed))
            {
                normalizedScheduleCodes = null;
                validationMessage = "positionScheduleCodes may only include fulltime and parttime.";
                return false;
            }

            var normalized = EuresSearchFilterCodes.CanonicalPositionScheduleCode(trimmed);
            if (!canonical.Contains(normalized, StringComparer.Ordinal))
            {
                canonical.Add(normalized);
            }
        }

        if (canonical.Count == 0)
        {
            normalizedScheduleCodes = null;
            validationMessage = string.Empty;
            return true;
        }

        if (canonical.Count > EuresSearchFilterCodes.MaxPositionScheduleCodes)
        {
            normalizedScheduleCodes = null;
            validationMessage =
                $"positionScheduleCodes accepts at most {EuresSearchFilterCodes.MaxPositionScheduleCodes} values.";
            return false;
        }

        canonical.Sort(StringComparer.Ordinal);
        normalizedScheduleCodes = canonical;
        validationMessage = string.Empty;
        return true;
    }
}
