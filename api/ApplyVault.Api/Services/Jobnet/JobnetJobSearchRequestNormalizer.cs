using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Jobnet;

public sealed class JobnetJobSearchRequestNormalizer(IOptions<JobnetIntegrationOptions> options)
    : IJobnetJobSearchRequestNormalizer
{
    public bool TryNormalizeSearchRequest(
        JobnetJobSearchRequest request,
        out JobnetJobSearchRequest normalizedRequest,
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

        normalizedRequest = new JobnetJobSearchRequest
        {
            Keywords = normalizedKeywords.ToList(),
            Keyword = normalizedKeywords.Length == 1 ? normalizedKeywords[0] : null,
            Page = Math.Max(1, request.Page),
            ResultsPerPage = cappedResults,
            RequestLanguage = NormalizeRequestLanguage(request.RequestLanguage)
        };

        validationMessage = string.Empty;
        return true;
    }

    public string NormalizeRequestLanguage(string? requestLanguage)
    {
        return string.IsNullOrWhiteSpace(requestLanguage) ? "en" : requestLanguage.Trim();
    }
}
