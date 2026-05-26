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
            SortSearch = normalizedKeywords.Length > 1
                ? "BEST_MATCH"
                : string.IsNullOrWhiteSpace(request.SortSearch)
                    ? "MOST_RECENT"
                    : request.SortSearch.Trim()
        };

        validationMessage = string.Empty;
        return true;
    }

    public string NormalizeRequestLanguage(string? requestLanguage)
    {
        return string.IsNullOrWhiteSpace(requestLanguage) ? "en" : requestLanguage.Trim();
    }
}
