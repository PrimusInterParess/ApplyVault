using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Eures;

internal sealed record EuresRankedSearchSnapshot(
    EuresJobListingDto[] Jobs,
    int? UpstreamTotalRecords,
    bool ResultsTruncated);
