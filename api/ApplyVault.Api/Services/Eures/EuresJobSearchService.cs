using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Eures;

internal sealed class EuresJobSearchService(
    EuresApiClient apiClient,
    IOptions<EuresIntegrationOptions> options,
    IMemoryCache cache)
{
    private static readonly TimeSpan RankedResultsCacheLifetime = TimeSpan.FromMinutes(5);

    public async Task<EuresJobSearchResponse> SearchAsync(
        EuresJobSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var integrationOptions = options.Value;
        var keywords = request.ResolveKeywords();
        var euresSearchTerms = EuresKeywordSearchExpander.ExpandSearchTerms(keywords);
        var locationCode = string.IsNullOrWhiteSpace(request.LocationCode)
            ? integrationOptions.DefaultLocationCode
            : request.LocationCode.Trim();
        var sortSearch = ResolveSortSearch(request.SortSearch, euresSearchTerms.Length > 1);
        var cacheKey = BuildCacheKey(
            keywords,
            euresSearchTerms,
            locationCode,
            sortSearch,
            request.RequestLanguage);
        var sessionId = BuildSessionId(cacheKey);

        var rankedJobs = await cache.GetOrCreateAsync(
            cacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = RankedResultsCacheLifetime;

                if (euresSearchTerms.Length == 1 && keywords.Count == 1)
                {
                    return await FetchSingleKeywordRankedJobsAsync(
                        keywords,
                        euresSearchTerms[0],
                        request,
                        locationCode,
                        sortSearch,
                        sessionId,
                        integrationOptions,
                        cancellationToken);
                }

                return await FetchKeywordUnionRankedJobsAsync(
                    keywords,
                    euresSearchTerms,
                    request,
                    locationCode,
                    sortSearch,
                    sessionId,
                    integrationOptions,
                    cancellationToken);
            }) ?? [];

        return PaginateResults(rankedJobs, request.Page, request.ResultsPerPage);
    }

    private async Task<EuresJobListingDto[]> FetchSingleKeywordRankedJobsAsync(
        IReadOnlyList<string> userKeywords,
        string euresSearchTerm,
        EuresJobSearchRequest request,
        string locationCode,
        string sortSearch,
        string sessionId,
        EuresIntegrationOptions integrationOptions,
        CancellationToken cancellationToken)
    {
        var fetchSize = Math.Max(request.ResultsPerPage, integrationOptions.MaxResultsPerPage);
        var searchResponse = await apiClient.SearchAsync(
            EuresApiClient.BuildSearchPayload(
                euresSearchTerm,
                fetchSize,
                page: 1,
                sortSearch,
                locationCode,
                request.RequestLanguage,
                sessionId),
            cancellationToken);

        return RankJobs(searchResponse, userKeywords, request.RequestLanguage);
    }

    private async Task<EuresJobListingDto[]> FetchKeywordUnionRankedJobsAsync(
        IReadOnlyList<string> userKeywords,
        IReadOnlyList<string> euresSearchTerms,
        EuresJobSearchRequest request,
        string locationCode,
        string sortSearch,
        string sessionId,
        EuresIntegrationOptions integrationOptions,
        CancellationToken cancellationToken)
    {
        var fetchPerKeyword = Math.Max(request.ResultsPerPage, integrationOptions.MaxResultsPerPage);
        var searchTasks = euresSearchTerms
            .Select((searchTerm) => apiClient.SearchAsync(
                EuresApiClient.BuildSearchPayload(
                    searchTerm,
                    fetchPerKeyword,
                    page: 1,
                    sortSearch,
                    locationCode,
                    request.RequestLanguage,
                    sessionId),
                cancellationToken))
            .ToArray();

        var searchResponses = await Task.WhenAll(searchTasks);
        var mergedJobs = new Dictionary<string, RankedJobListing>(StringComparer.OrdinalIgnoreCase);

        foreach (var searchResponse in searchResponses)
        {
            MergeRankedJobs(mergedJobs, searchResponse, userKeywords, request.RequestLanguage);
        }

        return mergedJobs.Values
            .OrderByDescending((entry) => entry.RelevanceScore)
            .ThenByDescending((entry) => entry.CreationDate)
            .Select((entry) => entry.Listing)
            .ToArray();
    }

    private static string BuildCacheKey(
        IReadOnlyList<string> keywords,
        IReadOnlyList<string> euresSearchTerms,
        string locationCode,
        string sortSearch,
        string requestLanguage)
    {
        var keywordFingerprint = string.Join('\u001f', keywords.OrderBy((keyword) => keyword, StringComparer.OrdinalIgnoreCase));
        var termFingerprint = string.Join('\u001f', euresSearchTerms.OrderBy((term) => term, StringComparer.OrdinalIgnoreCase));

        return string.Join(
            '\u001e',
            keywordFingerprint,
            termFingerprint,
            locationCode.Trim().ToLowerInvariant(),
            sortSearch.Trim().ToUpperInvariant(),
            requestLanguage.Trim().ToLowerInvariant());
    }

    private static string BuildSessionId(string cacheKey)
    {
        var hash = cacheKey.GetHashCode(StringComparison.Ordinal);
        return $"applyvault-{hash:X8}";
    }

    private static EuresJobListingDto[] RankJobs(
        EuresSearchResponsePayload? searchResponse,
        IReadOnlyList<string> userKeywords,
        string requestLanguage)
    {
        return searchResponse?.Jvs?
            .Select((job) => CreateRankedListing(job, userKeywords, requestLanguage))
            .Where((entry) => entry.RelevanceScore > 0)
            .OrderByDescending((entry) => entry.RelevanceScore)
            .ThenByDescending((entry) => entry.CreationDate)
            .Select((entry) => entry.Listing)
            .ToArray() ?? [];
    }

    private static void MergeRankedJobs(
        Dictionary<string, RankedJobListing> mergedJobs,
        EuresSearchResponsePayload? searchResponse,
        IReadOnlyList<string> userKeywords,
        string requestLanguage)
    {
        if (searchResponse?.Jvs is null)
        {
            return;
        }

        foreach (var job in searchResponse.Jvs)
        {
            if (string.IsNullOrWhiteSpace(job.Id))
            {
                continue;
            }

            var rankedListing = CreateRankedListing(job, userKeywords, requestLanguage);
            if (rankedListing.RelevanceScore <= 0)
            {
                continue;
            }

            if (mergedJobs.TryGetValue(job.Id, out var existing)
                && rankedListing.RelevanceScore <= existing.RelevanceScore)
            {
                continue;
            }

            mergedJobs[job.Id] = rankedListing;
        }
    }

    private static RankedJobListing CreateRankedListing(
        EuresSearchJobPayload job,
        IReadOnlyList<string> userKeywords,
        string requestLanguage)
    {
        var listing = EuresJobMapper.MapListing(job, requestLanguage);
        var profile = EuresJobMapper.ResolveSearchProfile(job, requestLanguage);
        var relevanceScore = EuresJobRelevanceScorer.CalculateScore(
            listing.Title,
            profile?.Employer?.Name ?? job.Employer?.Name,
            profile?.Description,
            userKeywords);

        return new RankedJobListing(listing, relevanceScore, job.CreationDate ?? 0);
    }

    private static EuresJobSearchResponse PaginateResults(
        IReadOnlyList<EuresJobListingDto> rankedJobs,
        int page,
        int resultsPerPage)
    {
        var skip = (page - 1) * resultsPerPage;
        var pageJobs = rankedJobs
            .Skip(skip)
            .Take(resultsPerPage)
            .ToArray();

        return new EuresJobSearchResponse(
            rankedJobs.Count,
            page,
            resultsPerPage,
            pageJobs);
    }

    private static string ResolveSortSearch(string? sortSearch, bool multipleKeywords)
    {
        if (multipleKeywords)
        {
            return "BEST_MATCH";
        }

        return string.IsNullOrWhiteSpace(sortSearch) ? "MOST_RECENT" : sortSearch.Trim();
    }

    private sealed record RankedJobListing(
        EuresJobListingDto Listing,
        int RelevanceScore,
        long CreationDate);
}
