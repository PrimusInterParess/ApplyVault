using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetJobSearchService(
    JobnetApiClient apiClient,
    IOptions<JobnetIntegrationOptions> options,
    JobnetRankedResultsCache rankedResultsCache,
    JobnetClassificationCache classificationCache)
{
    public async Task<JobnetJobSearchResponse> SearchAsync(
        JobnetJobSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var integrationOptions = options.Value;
        var keywords = request.ResolveKeywords();
        var searchString = string.Join(' ', keywords);
        var cacheKey = BuildCacheKey(searchString, integrationOptions.WorkInDenmarkOnly, request.RequestLanguage);

        var rankedSnapshot = await rankedResultsCache.GetOrCreateAsync(
            cacheKey,
            (ct) => FetchRankedJobsAsync(searchString, keywords, integrationOptions, ct),
            cancellationToken);

        return PaginateResults(
            rankedSnapshot,
            request.Page,
            request.ResultsPerPage);
    }

    private async Task<JobnetRankedSearchSnapshot> FetchRankedJobsAsync(
        string searchString,
        IReadOnlyList<string> keywords,
        JobnetIntegrationOptions integrationOptions,
        CancellationToken cancellationToken)
    {
        var fetchSize = Math.Clamp(
            integrationOptions.ScanResultsPerPage,
            1,
            integrationOptions.MaxResultsPerPage);
        var maxPages = Math.Max(1, integrationOptions.MaxUpstreamScanPages);
        var maxCachedResults = Math.Max(1, integrationOptions.MaxCachedRankedResults);
        var maxDetailFetches = Math.Max(1, integrationOptions.MaxClassificationDetailFetches);
        var detailConcurrency = Math.Clamp(integrationOptions.MaxDetailFetchConcurrency, 1, 16);
        var trustUpstreamMatches = keywords.Count == 1;

        var rankedEntries = new Dictionary<string, RankedJobListing>(StringComparer.OrdinalIgnoreCase);
        var detailFetchCount = 0;
        int? upstreamTotalJobAdCount = null;
        var hitSafetyCap = false;
        using var detailConcurrencyGate = new SemaphoreSlim(detailConcurrency, detailConcurrency);

        for (var pageNumber = 1; pageNumber <= maxPages; pageNumber++)
        {
            var searchResponse = await apiClient.SearchAsync(
                searchString,
                pageNumber,
                fetchSize,
                cancellationToken);

            upstreamTotalJobAdCount ??= searchResponse?.TotalJobAdCount;
            var jobsOnPage = searchResponse?.JobAds?.Count ?? 0;

            if (integrationOptions.WorkInDenmarkOnly)
            {
                MergeEuresImportedJobs(
                    searchResponse?.JobAds ?? [],
                    keywords,
                    rankedEntries,
                    trustUpstreamMatches,
                    maxCachedResults);

                if (detailFetchCount < maxDetailFetches && rankedEntries.Count < maxCachedResults)
                {
                    detailFetchCount += await MergeVerifiedGuidJobsAsync(
                        searchResponse?.JobAds ?? [],
                        keywords,
                        rankedEntries,
                        detailConcurrencyGate,
                        maxDetailFetches - detailFetchCount,
                        trustUpstreamMatches,
                        maxCachedResults,
                        cancellationToken);
                }
            }
            else
            {
                MergeUnfilteredJobs(
                    searchResponse?.JobAds ?? [],
                    keywords,
                    rankedEntries,
                    trustUpstreamMatches,
                    maxCachedResults);
            }

            if (ShouldStopUpstreamScan(
                    rankedEntries.Count,
                    upstreamTotalJobAdCount,
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

        var rankedJobs = rankedEntries.Values
            .OrderByDescending((entry) => entry.RelevanceScore)
            .ThenByDescending((entry) => entry.PublicationSortKey)
            .Select((entry) => entry.Listing)
            .ToArray();

        var resultsTruncated = hitSafetyCap
            || (upstreamTotalJobAdCount is > 0 && rankedJobs.Length < upstreamTotalJobAdCount.Value);

        return new JobnetRankedSearchSnapshot(rankedJobs, upstreamTotalJobAdCount, resultsTruncated);
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

    private static void MergeEuresImportedJobs(
        IReadOnlyList<JobnetSearchJobPayload> jobs,
        IReadOnlyList<string> keywords,
        Dictionary<string, RankedJobListing> rankedEntries,
        bool trustUpstreamMatches,
        int maxCachedResults)
    {
        foreach (var job in jobs)
        {
            if (rankedEntries.Count >= maxCachedResults)
            {
                break;
            }

            if (!JobnetJobIdentifiers.IsEuresImported(job.JobAdId))
            {
                continue;
            }

            TryAddRankedJob(rankedEntries, job, keywords, workInDenmark: true, trustUpstreamMatches);
        }
    }

    private async Task<int> MergeVerifiedGuidJobsAsync(
        IReadOnlyList<JobnetSearchJobPayload> jobs,
        IReadOnlyList<string> keywords,
        Dictionary<string, RankedJobListing> rankedEntries,
        SemaphoreSlim detailConcurrencyGate,
        int remainingDetailFetches,
        bool trustUpstreamMatches,
        int maxCachedResults,
        CancellationToken cancellationToken)
    {
        if (remainingDetailFetches <= 0 || rankedEntries.Count >= maxCachedResults)
        {
            return 0;
        }

        var candidates = jobs
            .Where((job) => !string.IsNullOrWhiteSpace(job.JobAdId))
            .Where((job) => JobnetJobIdentifiers.SupportsNativeDetailEndpoint(job.JobAdId))
            .Where((job) => trustUpstreamMatches || PassesRelevancePreFilter(job, keywords))
            .Take(remainingDetailFetches)
            .ToArray();

        if (candidates.Length == 0)
        {
            return 0;
        }

        var classificationTasks = candidates.Select(async (job) =>
        {
            var cached = await classificationCache.GetWorkInDenmarkAsync(job.JobAdId!, cancellationToken)
                .ConfigureAwait(false);

            if (cached.HasValue)
            {
                return (Job: job, IsWorkInDenmark: cached, FetchedDetail: false);
            }

            await detailConcurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var isWorkInDenmark = await ResolveWorkInDenmarkFromApiAsync(job.JobAdId!, cancellationToken)
                    .ConfigureAwait(false);

                return (Job: job, IsWorkInDenmark: isWorkInDenmark, FetchedDetail: true);
            }
            finally
            {
                detailConcurrencyGate.Release();
            }
        });

        var classifications = await Task.WhenAll(classificationTasks).ConfigureAwait(false);

        foreach (var (job, isWorkInDenmark, _) in classifications)
        {
            if (rankedEntries.Count >= maxCachedResults)
            {
                break;
            }

            if (isWorkInDenmark != true || string.IsNullOrWhiteSpace(job.JobAdId))
            {
                continue;
            }

            TryAddRankedJob(rankedEntries, job, keywords, workInDenmark: true, trustUpstreamMatches);
        }

        return classifications.Count((entry) => entry.FetchedDetail);
    }

    private void MergeUnfilteredJobs(
        IReadOnlyList<JobnetSearchJobPayload> jobs,
        IReadOnlyList<string> keywords,
        Dictionary<string, RankedJobListing> rankedEntries,
        bool trustUpstreamMatches,
        int maxCachedResults)
    {
        foreach (var job in jobs)
        {
            if (rankedEntries.Count >= maxCachedResults)
            {
                break;
            }

            TryAddRankedJob(rankedEntries, job, keywords, workInDenmark: true, trustUpstreamMatches);
        }
    }

    private async Task<bool?> ResolveWorkInDenmarkFromApiAsync(
        string jobAdId,
        CancellationToken cancellationToken)
    {
        var detail = await apiClient.GetJobByIdAsync(jobAdId, cancellationToken).ConfigureAwait(false);
        var isWorkInDenmark = detail is not null
            && JobnetJobMapper.HasWorkInDenmarkClassification(detail.Job?.Classifications);

        await classificationCache.SetWorkInDenmarkAsync(jobAdId, isWorkInDenmark, cancellationToken)
            .ConfigureAwait(false);

        return isWorkInDenmark;
    }

    private static bool PassesRelevancePreFilter(JobnetSearchJobPayload job, IReadOnlyList<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return true;
        }

        return JobnetJobRelevanceScorer.CalculateScore(
            job.Title,
            job.HiringOrgName,
            job.Description,
            keywords) > 0;
    }

    private static void TryAddRankedJob(
        Dictionary<string, RankedJobListing> rankedEntries,
        JobnetSearchJobPayload job,
        IReadOnlyList<string> keywords,
        bool workInDenmark,
        bool trustUpstreamMatches)
    {
        if (string.IsNullOrWhiteSpace(job.JobAdId))
        {
            return;
        }

        var listing = JobnetJobMapper.MapListing(job, workInDenmark);
        var relevanceScore = keywords.Count == 0
            ? 1
            : JobnetJobRelevanceScorer.CalculateScore(
                listing.Title,
                listing.Employer,
                job.Description,
                keywords);

        if (!trustUpstreamMatches && keywords.Count > 0 && relevanceScore <= 0)
        {
            return;
        }

        if (trustUpstreamMatches && relevanceScore <= 0)
        {
            relevanceScore = 1;
        }

        var publicationSortKey = ParsePublicationSortKey(job.PublicationDate);

        if (rankedEntries.TryGetValue(job.JobAdId, out var existing)
            && existing.RelevanceScore >= relevanceScore)
        {
            return;
        }

        rankedEntries[job.JobAdId] = new RankedJobListing(listing, relevanceScore, publicationSortKey);
    }

    private static JobnetJobSearchResponse PaginateResults(
        JobnetRankedSearchSnapshot rankedSnapshot,
        int page,
        int resultsPerPage)
    {
        var rankedJobs = rankedSnapshot.Jobs;
        var skip = (page - 1) * resultsPerPage;
        var pageJobs = rankedJobs
            .Skip(skip)
            .Take(resultsPerPage)
            .ToArray();

        return new JobnetJobSearchResponse(
            rankedJobs.Length,
            page,
            resultsPerPage,
            pageJobs,
            rankedSnapshot.UpstreamTotalJobAdCount,
            rankedSnapshot.ResultsTruncated);
    }

    private static string BuildCacheKey(
        string searchString,
        bool workInDenmarkOnly,
        string requestLanguage)
    {
        return string.Join(
            '\u001e',
            searchString.Trim().ToLowerInvariant(),
            workInDenmarkOnly ? "wid" : "all",
            requestLanguage.Trim().ToLowerInvariant());
    }

    private static long ParsePublicationSortKey(string? publicationDate)
    {
        return DateTimeOffset.TryParse(publicationDate, out var parsed)
            ? parsed.ToUnixTimeMilliseconds()
            : 0;
    }

    private sealed record RankedJobListing(
        JobnetJobListingDto Listing,
        int RelevanceScore,
        long PublicationSortKey);
}
