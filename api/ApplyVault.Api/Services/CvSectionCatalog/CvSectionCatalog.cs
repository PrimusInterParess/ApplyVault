using System.Text.Json;

using ApplyVault.Api.Services;

namespace ApplyVault.Api.Services.CvSectionCatalog;

public interface ICvSectionCatalog
{
    int Version { get; }

    IReadOnlyList<CvSectionTypeDefinition> SectionTypes { get; }

    CvSectionTypeDefinition GetRequired(string sectionTypeId);

    bool IsKnown(string? sectionTypeId);

    string Normalize(string? sectionTypeId);

    string MapHeadingAliasToSectionType(string normalizedHeadingAlias);

    bool TryMatchSectionHeading(string text, out string normalizedAlias);

    string BuildImportSystemPrompt();

    CvSectionCatalogDto ToApiDto();
}

public sealed class CvSectionCatalogProvider : ICvSectionCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CvSectionCatalogDocument _document;
    private readonly IReadOnlyDictionary<string, CvSectionTypeDefinition> _byId;
    private readonly IReadOnlyDictionary<string, string> _aliasToSectionType;
    private readonly IReadOnlyList<string> _headingPatternsLongestFirst;

    public CvSectionCatalogProvider(CvSectionCatalogDocument document)
    {
        _document = document;
        _byId = document.SectionTypes.ToDictionary((type) => type.Id, StringComparer.OrdinalIgnoreCase);

        var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var patterns = new List<string>();

        foreach (var sectionType in document.SectionTypes)
        {
            foreach (var alias in sectionType.HeadingAliases)
            {
                var normalized = CvSectionHeadingNormalizer.Normalize(alias);
                if (normalized.Length == 0)
                {
                    continue;
                }

                aliasMap.TryAdd(normalized, sectionType.Id);
                patterns.Add(normalized);
            }
        }

        _aliasToSectionType = aliasMap;
        _headingPatternsLongestFirst = patterns
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending((pattern) => pattern.Length)
            .ToArray();
    }

    public static CvSectionCatalogProvider LoadFromDefaultPath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var path = Path.Combine(baseDirectory, "CvSectionCatalog", "cv-section-catalog.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"CV section catalog not found at {path}.");
        }

        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<CvSectionCatalogDocument>(json, SerializerOptions)
            ?? throw new InvalidOperationException("CV section catalog JSON is empty.");

        return new CvSectionCatalogProvider(document);
    }

    public int Version => _document.Version;

    public IReadOnlyList<CvSectionTypeDefinition> SectionTypes => _document.SectionTypes;

    public CvSectionTypeDefinition GetRequired(string sectionTypeId) =>
        _byId.TryGetValue(sectionTypeId, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown section type '{sectionTypeId}'.");

    public bool IsKnown(string? sectionTypeId) =>
        !string.IsNullOrWhiteSpace(sectionTypeId) && _byId.ContainsKey(sectionTypeId);

    public string Normalize(string? sectionTypeId) =>
        IsKnown(sectionTypeId) ? sectionTypeId! : CvSectionTypes.Custom;

    public string MapHeadingAliasToSectionType(string normalizedHeadingAlias)
    {
        var normalized = CvSectionHeadingNormalizer.Normalize(normalizedHeadingAlias);

        return _aliasToSectionType.TryGetValue(normalized, out var sectionTypeId)
            ? sectionTypeId
            : CvSectionTypes.Custom;
    }

    public bool TryMatchSectionHeading(string text, out string normalizedAlias)
    {
        normalizedAlias = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = CvSectionHeadingNormalizer.Normalize(text);

        if (trimmed.Length > 64)
        {
            return false;
        }

        foreach (var pattern in _headingPatternsLongestFirst)
        {
            if (trimmed.Equals(pattern, StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith($"{pattern} ", StringComparison.OrdinalIgnoreCase))
            {
                normalizedAlias = pattern;
                return true;
            }
        }

        return false;
    }

    public string BuildImportSystemPrompt()
    {
        var sectionTypeList = string.Join(", ", _document.SectionTypes.Select((type) => type.Id));

        var headingRules = _document.SectionTypes
            .SelectMany((type) => type.HeadingAliases.Select((alias) => (Alias: alias, type.Id)))
            .GroupBy((item) => item.Id)
            .Select((group) =>
                $"- {string.Join(" / ", group.Select((item) => item.Alias).Distinct())} -> {group.Key}")
            .ToArray();

        var fieldRules = _document.SectionTypes
            .Select((type) =>
            {
                var fields = string.Join(
                    ", ",
                    type.EntryFields.Select((field) => $"{field.Id} ({field.Label})"));
                var hints = string.IsNullOrWhiteSpace(type.ImportHints) ? string.Empty : $" {type.ImportHints}";
                return $"- {type.Id}: fields [{fields}].{hints}";
            })
            .ToArray();

        return $"""
            You structure CV/resume text extracted from a PDF into editable sections and entries.
            Return JSON only. Do not wrap in markdown fences.
            Use only facts present in the source text. Do not invent employers, projects, dates, technologies, or achievements.
            Preserve the original order of sections and entries when possible.

            sectionType must be one of: {sectionTypeList}.

            Map headings:
            {string.Join(Environment.NewLine, headingRules)}
            - anything else -> Custom

            Wire format for each entry (map into section-specific fields using importKey rules below):
            - title, subtitle, dateRange, summary, bullets (string array), techStack (string)

            Per sectionType:
            {string.Join(Environment.NewLine, fieldRules)}

            Decisive rules:
            - One entry per job, project, or degree — never merge multiple roles into one entry
            - Put dates only in dateRange, never in title or subtitle
            - Put bullet-like lines in bullets, not in summary
            - Do not use markdown, HTML, or bold markers in any field
            - If contact lines appear before summary/profile text, split them into Contact and keep prose in Summary
            - If a raw section mixes experience and projects, split into separate sections with correct sectionType
            - Do not invent facts; improve structure only
            """;
    }

    public CvSectionCatalogDto ToApiDto() =>
        new(
            Version,
            SectionTypes
                .Select((type) => new CvSectionTypeCatalogDto(
                    type.Id,
                    type.DefaultHeading,
                    type.HeadingAliases,
                    type.EntryFields
                        .Select((field) => new CvSectionFieldCatalogDto(field.Id, field.Label, field.Kind))
                        .ToArray(),
                    type.ImportHints))
                .ToArray());
}

public static class CvSectionHeadingNormalizer
{
    internal static string Normalize(string text)
    {
        var trimmed = text.Trim();
        trimmed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", " ");
        trimmed = trimmed.Trim(':', '.', '-', '–', '—', '•', '·', '|', ' ');
        trimmed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"^[|\-–—•·\s]+", string.Empty);
        trimmed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"[|\-–—•·\s]+$", string.Empty);

        return trimmed.Trim();
    }
}
