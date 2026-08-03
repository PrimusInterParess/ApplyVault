using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Eures;

internal sealed class EuresJobSearchService(
    EuresApiClient apiClient,
    IOptions<EuresIntegrationOptions> options,
    EuresRankedResultsCache rankedResultsCache)
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
        var sortSearch = ResolveSortSearch(request.SortSearch, euresSearchTerms.Length > 1);
        var cacheKey = BuildCacheKey(
            keywords,
            euresSearchTerms,
            locationCode,
            sortSearch,
            request.RequestLanguage,
            request.PublicationPeriod,
            request.PositionScheduleCodes);
        var sessionId = BuildSessionId(cacheKey);

        var rankedSnapshot = await rankedResultsCache.GetOrCreateAsync(
            cacheKey,
            async (ct) =>
            {
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
                        ct);
                }

                return await FetchKeywordUnionRankedJobsAsync(
                    keywords,
                    euresSearchTerms,
                    request,
                    locationCode,
                    sortSearch,
                    sessionId,
                    integrationOptions,
                    ct);
            },
            cancellationToken);

        return PaginateResults(
            rankedSnapshot,
            request.Page,
            request.ResultsPerPage);
    }

    private async Task<EuresRankedSearchSnapshot> FetchSingleKeywordRankedJobsAsync(
        IReadOnlyList<string> userKeywords,
        string euresSearchTerm,
        EuresJobSearchRequest request,
        string locationCode,
        string sortSearch,
        string sessionId,
        EuresIntegrationOptions integrationOptions,
        CancellationToken cancellationToken)
    {
        var fetchSize = Math.Clamp(integrationOptions.MaxResultsPerPage, 1, 100);
        var maxPages = Math.Max(1, integrationOptions.MaxUpstreamScanPages);
        var maxCachedResults = Math.Max(1, integrationOptions.MaxCachedRankedResults);
        var mergedJobs = new Dictionary<string, RankedJobListing>(StringComparer.OrdinalIgnoreCase);
        int? upstreamTotalRecords = null;
        var hitSafetyCap = false;

        for (var pageNumber = 1; pageNumber <= maxPages; pageNumber++)
        {
            var searchResponse = await apiClient.SearchAsync(
                EuresApiClient.BuildSearchPayload(
                    euresSearchTerm,
                    fetchSize,
                    pageNumber,
                    sortSearch,
                    locationCode,
                    request.RequestLanguage,
                    sessionId,
                    request.PublicationPeriod,
                    request.PositionScheduleCodes),
                cancellationToken);

            upstreamTotalRecords ??= searchResponse?.NumberRecords;
            var jobsOnPage = searchResponse?.Jvs?.Count ?? 0;

            MergeRankedJobs(
                mergedJobs,
                searchResponse,
                userKeywords,
                request.RequestLanguage,
                trustUpstreamMatches: true,
                maxCachedResults);

            if (ShouldStopUpstreamScan(
                    mergedJobs.Count,
                    upstreamTotalRecords,
                    pageNumber,
                    fetchSize,
                    jobsOnPage,
                    maxPages,
                    maxCachedResults,
                    out hitSafetyCap))
            {
                break;
            }
        }

        return BuildSnapshot(mergedJobs, sortSearch, upstreamTotalRecords, hitSafetyCap);
    }

    private async Task<EuresRankedSearchSnapshot> FetchKeywordUnionRankedJobsAsync(
        IReadOnlyList<string> userKeywords,
        IReadOnlyList<string> euresSearchTerms,
        EuresJobSearchRequest request,
        string locationCode,
        string sortSearch,
        string sessionId,
        EuresIntegrationOptions integrationOptions,
        CancellationToken cancellationToken)
    {
        var fetchPerKeyword = Math.Clamp(integrationOptions.MaxResultsPerPage, 1, 100);
        var maxCachedResults = Math.Max(1, integrationOptions.MaxCachedRankedResults);
        var searchTasks = euresSearchTerms
            .Select((searchTerm) => apiClient.SearchAsync(
                EuresApiClient.BuildSearchPayload(
                    searchTerm,
                    fetchPerKeyword,
                    page: 1,
                    sortSearch,
                    locationCode,
                    request.RequestLanguage,
                    sessionId,
                    request.PublicationPeriod,
                    request.PositionScheduleCodes),
                cancellationToken))
            .ToArray();

        var searchResponses = await Task.WhenAll(searchTasks);
        var mergedJobs = new Dictionary<string, RankedJobListing>(StringComparer.OrdinalIgnoreCase);

        foreach (var searchResponse in searchResponses)
        {
            MergeRankedJobs(
                mergedJobs,
                searchResponse,
                userKeywords,
                request.RequestLanguage,
                trustUpstreamMatches: false,
                maxCachedResults);
        }

        return BuildSnapshot(mergedJobs, sortSearch, upstreamTotalRecords: null, resultsTruncated: false);
    }

    private static EuresRankedSearchSnapshot BuildSnapshot(
        Dictionary<string, RankedJobListing> mergedJobs,
        string sortSearch,
        int? upstreamTotalRecords,
        bool resultsTruncated)
    {
        // Local re-rank must honor the resolved sort. MOST_RECENT was previously always
        // overridden by relevance, which floated older high-match jobs above fresher ones.
        IEnumerable<RankedJobListing> ordered = string.Equals(
            sortSearch,
            "MOST_RECENT",
            StringComparison.OrdinalIgnoreCase)
            ? mergedJobs.Values
                .OrderByDescending((entry) => entry.CreationDate)
                .ThenByDescending((entry) => entry.RelevanceScore)
            : mergedJobs.Values
                .OrderByDescending((entry) => entry.RelevanceScore)
                .ThenByDescending((entry) => entry.CreationDate);

        var rankedJobs = ordered
            .Select((entry) => entry.Listing)
            .ToArray();

        var truncated = resultsTruncated
            || (upstreamTotalRecords is > 0 && rankedJobs.Length < upstreamTotalRecords.Value);

        return new EuresRankedSearchSnapshot(rankedJobs, upstreamTotalRecords, truncated);
    }

    private static bool ShouldStopUpstreamScan(
        int rankedCount,
        int? upstreamTotal,
        int pageNumber,
        int fetchSize,
        int jobsOnPage,
        int maxPages,
        int maxCachedResults,
        out bool hitSafetyCap)
    {
        hitSafetyCap = false;

        if (jobsOnPage == 0)
        {
            return true;
        }

        if (rankedCount >= maxCachedResults)
        {
            hitSafetyCap = true;
            return true;
        }

        if (jobsOnPage < fetchSize)
        {
            return true;
        }

        if (upstreamTotal is > 0 && rankedCount >= upstreamTotal.Value)
        {
            return true;
        }

        if (upstreamTotal is > 0 && pageNumber * fetchSize >= upstreamTotal.Value)
        {
            return true;
        }

        if (pageNumber >= maxPages)
        {
            hitSafetyCap = true;
            return true;
        }

        return false;
    }

    private static string BuildCacheKey(
        IReadOnlyList<string> keywords,
        IReadOnlyList<string> euresSearchTerms,
        string locationCode,
        string sortSearch,
        string requestLanguage,
        string? publicationPeriod,
        IReadOnlyList<string>? positionScheduleCodes)
    {
        var keywordFingerprint = string.Join('\u001f', keywords.OrderBy((keyword) => keyword, StringComparer.OrdinalIgnoreCase));
        var termFingerprint = string.Join('\u001f', euresSearchTerms.OrderBy((term) => term, StringComparer.OrdinalIgnoreCase));
        var publicationFingerprint = string.IsNullOrWhiteSpace(publicationPeriod)
            ? string.Empty
            : publicationPeriod.Trim().ToUpperInvariant();
        var scheduleFingerprint = positionScheduleCodes is null || positionScheduleCodes.Count == 0
            ? string.Empty
            : string.Join(
                '\u001f',
                positionScheduleCodes
                    .Where((code) => !string.IsNullOrWhiteSpace(code))
                    .Select((code) => code.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy((code) => code, StringComparer.Ordinal));

        return string.Join(
            '\u001e',
            keywordFingerprint,
            termFingerprint,
            locationCode.Trim().ToLowerInvariant(),
            sortSearch.Trim().ToUpperInvariant(),
            requestLanguage.Trim().ToLowerInvariant(),
            publicationFingerprint,
            scheduleFingerprint);
    }

    private static string BuildSessionId(string cacheKey)
    {
        var hash = cacheKey.GetHashCode(StringComparison.Ordinal);
        return $"applyvault-{hash:X8}";
    }

    private static void MergeRankedJobs(
        Dictionary<string, RankedJobListing> mergedJobs,
        EuresSearchResponsePayload? searchResponse,
        IReadOnlyList<string> userKeywords,
        string requestLanguage,
        bool trustUpstreamMatches,
        int maxCachedResults)
    {
        if (searchResponse?.Jvs is null || mergedJobs.Count >= maxCachedResults)
        {
            return;
        }

        foreach (var job in searchResponse.Jvs)
        {
            if (mergedJobs.Count >= maxCachedResults)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(job.Id))
            {
                continue;
            }

            var rankedListing = CreateRankedListing(job, userKeywords, requestLanguage);

            if (!trustUpstreamMatches && rankedListing.RelevanceScore <= 0)
            {
                continue;
            }

            if (trustUpstreamMatches && rankedListing.RelevanceScore <= 0)
            {
                rankedListing = rankedListing with { RelevanceScore = 1 };
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
        EuresRankedSearchSnapshot rankedSnapshot,
        int page,
        int resultsPerPage)
    {
        var rankedJobs = rankedSnapshot.Jobs;
        var skip = (page - 1) * resultsPerPage;
        var pageJobs = rankedJobs
            .Skip(skip)
            .Take(resultsPerPage)
            .ToArray();

        return new EuresJobSearchResponse(
            rankedJobs.Length,
            page,
            resultsPerPage,
            pageJobs,
            rankedSnapshot.UpstreamTotalRecords,
            rankedSnapshot.ResultsTruncated);
    }

    private static string ResolveSortSearch(string? sortSearch, bool multipleKeywords)
    {
        if (multipleKeywords)
        {
            return "BEST_MATCH";
        }

        if (string.IsNullOrWhiteSpace(sortSearch))
        {
            return "MOST_RECENT";
        }

        var trimmed = sortSearch.Trim();
        return EuresSearchFilterCodes.SortSearch.Contains(trimmed)
            ? EuresSearchFilterCodes.CanonicalSortSearch(trimmed)
            : "MOST_RECENT";
    }

    private sealed record RankedJobListing(
        EuresJobListingDto Listing,
        int RelevanceScore,
        long CreationDate);
}
