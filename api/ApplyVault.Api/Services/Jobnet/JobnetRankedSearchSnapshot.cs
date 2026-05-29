using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Jobnet;

internal sealed record JobnetRankedSearchSnapshot(
    JobnetJobListingDto[] Jobs,
    int? UpstreamTotalJobAdCount,
    bool ResultsTruncated);
