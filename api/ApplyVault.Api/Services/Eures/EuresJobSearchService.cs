using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Eures;

internal sealed class EuresJobSearchService(
    EuresApiClient apiClient,
    IOptions<EuresIntegrationOptions> options)
{
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
        var fetchSize = Math.Max(request.ResultsPerPage, integrationOptions.MaxResultsPerPage);

        if (euresSearchTerms.Length == 1 && keywords.Count == 1)
        {
            return await SearchSingleKeywordPageAsync(
                keywords,
                euresSearchTerms[0],
                request,
                locationCode,
                fetchSize,
                cancellationToken);
        }

        return await SearchKeywordUnionAsync(
            keywords,
            euresSearchTerms,
            request,
            locationCode,
            fetchSize,
            cancellationToken);
    }

    private async Task<EuresJobSearchResponse> SearchSingleKeywordPageAsync(
        IReadOnlyList<string> userKeywords,
        string euresSearchTerm,
        EuresJobSearchRequest request,
        string locationCode,
        int fetchSize,
        CancellationToken cancellationToken)
    {
        var searchResponse = await apiClient.SearchAsync(
            EuresApiClient.BuildSearchPayload(
                euresSearchTerm,
                fetchSize,
                page: 1,
                ResolveSortSearch(request.SortSearch, multipleKeywords: false),
                locationCode,
                request.RequestLanguage),
            cancellationToken);

        var rankedJobs = RankJobs(searchResponse, userKeywords, request.RequestLanguage);
        return PaginateResults(rankedJobs, request.Page, request.ResultsPerPage);
    }

    private async Task<EuresJobSearchResponse> SearchKeywordUnionAsync(
        IReadOnlyList<string> userKeywords,
        IReadOnlyList<string> euresSearchTerms,
        EuresJobSearchRequest request,
        string locationCode,
        int fetchPerKeyword,
        CancellationToken cancellationToken)
    {
        var searchTasks = euresSearchTerms
            .Select((searchTerm) => apiClient.SearchAsync(
                EuresApiClient.BuildSearchPayload(
                    searchTerm,
                    fetchPerKeyword,
                    page: 1,
                    ResolveSortSearch(request.SortSearch, multipleKeywords: true),
                    locationCode,
                    request.RequestLanguage),
                cancellationToken))
            .ToArray();

        var searchResponses = await Task.WhenAll(searchTasks);
        var mergedJobs = new Dictionary<string, RankedJobListing>(StringComparer.OrdinalIgnoreCase);

        foreach (var searchResponse in searchResponses)
        {
            MergeRankedJobs(mergedJobs, searchResponse, userKeywords, request.RequestLanguage);
        }

        var rankedJobs = mergedJobs.Values
            .OrderByDescending((entry) => entry.RelevanceScore)
            .ThenByDescending((entry) => entry.CreationDate)
            .Select((entry) => entry.Listing)
            .ToArray();

        return PaginateResults(rankedJobs, request.Page, request.ResultsPerPage);
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
