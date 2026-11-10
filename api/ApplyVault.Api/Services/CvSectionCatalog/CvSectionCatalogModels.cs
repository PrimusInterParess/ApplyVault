using System.Text.Json.Serialization;

namespace ApplyVault.Api.Services.CvSectionCatalog;

public sealed class CvSectionCatalogDocument
{
    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("sectionTypes")]
    public List<CvSectionTypeDefinition> SectionTypes { get; init; } = [];

    /// <summary>
    /// Ordered fixed slots of the Structured CV (ADR-0001 SSOT). Exactly one slot per
    /// rendered section; users cannot add, remove, reorder, or rename them.
    /// </summary>
    [JsonPropertyName("fixedLayout")]
    public List<CvFixedSectionSlotDefinition> FixedLayout { get; init; } = [];
}

public sealed class CvFixedSectionSlotDefinition
{
    [JsonPropertyName("sectionType")]
    public required string SectionType { get; init; }

    [JsonPropertyName("heading")]
    public required string Heading { get; init; }
}

/// <summary>One fixed, ordered section slot of the Structured CV.</summary>
public sealed record CvFixedSectionSlot(string SectionType, string Heading);

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
    IReadOnlyList<CvSectionTypeCatalogDto> SectionTypes,
    IReadOnlyList<CvFixedSectionSlot> FixedLayout);

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
