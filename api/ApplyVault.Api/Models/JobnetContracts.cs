using System.Text.Json.Serialization;

namespace ApplyVault.Api.Models;

public sealed class JobnetJobSearchRequest
{
    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonPropertyName("keyword")]
    public string? Keyword { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("resultsPerPage")]
    public int ResultsPerPage { get; set; } = 20;

    public string RequestLanguage { get; set; } = "en";

    public IReadOnlyList<string> ResolveKeywords()
    {
        var resolvedKeywords = Keywords?
            .Select((keyword) => keyword.Trim())
            .Where((keyword) => keyword.Length > 0)
            .ToArray() ?? [];

        if (resolvedKeywords.Length > 0)
        {
            return resolvedKeywords;
        }

        if (string.IsNullOrWhiteSpace(Keyword))
        {
            return [];
        }

        return [Keyword.Trim()];
    }
}

public sealed record JobnetJobListingDto(
    string Id,
    string? Title,
    string? Employer,
    string? Location,
    string? PublicationDate,
    string? SourceUrl,
    bool WorkInDenmark
);

public sealed record JobnetJobSearchResponse(
    int TotalResults,
    int Page,
    int ResultsPerPage,
    IReadOnlyList<JobnetJobListingDto> Jobs,
    int? UpstreamTotalResults = null,
    bool ResultsTruncated = false);

public sealed record JobnetJobDetailResponse(
    string Id,
    string? Title,
    string? Employer,
    string? Location,
    string? PublicationDate,
    string? SourceUrl,
    string? Description,
    string? ApplicationUrl,
    string? ContractType,
    string? WorkHours,
    bool WorkInDenmark,
    string DescriptionSource,
    string DescriptionQuality,
    string? DescriptionExcerpt,
    string? DescriptionQualityReason
);

public sealed record SaveJobnetJobResponse(
    Guid Id,
    DateTimeOffset SavedAt,
    bool AlreadyExists
);
