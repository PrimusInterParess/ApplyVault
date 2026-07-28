using System.Text.Json.Serialization;

namespace ApplyVault.Api.Services.CvSectionCatalog;

public sealed class CvSectionCatalogDocument
{
    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("sectionTypes")]
    public List<CvSectionTypeDefinition> SectionTypes { get; init; } = [];
}

public sealed class CvSectionTypeDefinition
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("defaultHeading")]
    public required string DefaultHeading { get; init; }

    [JsonPropertyName("headingAliases")]
    public List<string> HeadingAliases { get; init; } = [];

    [JsonPropertyName("entryFields")]
    public List<CvSectionFieldDefinition> EntryFields { get; init; } = [];

    [JsonPropertyName("importHints")]
    public string? ImportHints { get; init; }
}

public sealed class CvSectionFieldDefinition
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("importKey")]
    public required string ImportKey { get; init; }
}

public sealed record CvSectionCatalogDto(
    int Version,
    IReadOnlyList<CvSectionTypeCatalogDto> SectionTypes);

public sealed record CvSectionTypeCatalogDto(
    string Id,
    string DefaultHeading,
    IReadOnlyList<string> HeadingAliases,
    IReadOnlyList<CvSectionFieldCatalogDto> EntryFields,
    string? ImportHints);

public sealed record CvSectionFieldCatalogDto(
    string Id,
    string Label,
    string Kind);
