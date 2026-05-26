using System.Text.Json.Serialization;

namespace ApplyVault.Api.Models;

public sealed class EuresJobSearchRequest
{
    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonPropertyName("keyword")]
    public string? Keyword { get; set; }

    public string? LocationCode { get; set; }

    public int Page { get; set; } = 1;

    public int ResultsPerPage { get; set; } = 20;

    public string RequestLanguage { get; set; } = "en";

    public string SortSearch { get; set; } = "MOST_RECENT";

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

public sealed record EuresJobListingDto(
    string Id,
    string? Title,
    string? Employer,
    string? Location,
    string? PublicationDate,
    string? SourceUrl
);

public sealed record EuresJobSearchResponse(
    int TotalResults,
    int Page,
    int ResultsPerPage,
    IReadOnlyList<EuresJobListingDto> Jobs
);

public sealed record EuresJobDetailResponse(
    string Id,
    string? Title,
    string? Employer,
    string? Location,
    string? PublicationDate,
    string? SourceUrl,
    string? Description,
    string? ApplicationUrl,
    string? ContractType,
    string? WorkHours
);

public sealed record SaveEuresJobResponse(
    Guid Id,
    DateTimeOffset SavedAt,
    bool AlreadyExists
);
