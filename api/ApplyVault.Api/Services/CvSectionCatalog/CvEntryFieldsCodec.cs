using System.Text.Json;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Services.CvSectionCatalog;

public static class CvEntryFieldsCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string SerializeFields(IReadOnlyDictionary<string, object?> fields) =>
        JsonSerializer.Serialize(fields, SerializerOptions);

    public static Dictionary<string, object?> DeserializeFields(string? fieldsJson)
    {
        if (string.IsNullOrWhiteSpace(fieldsJson))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(fieldsJson, SerializerOptions)
            ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, object?> FromImportEntry(
        ICvSectionCatalog catalog,
        string sectionType,
        CvStructuredImportEntryResult entry)
    {
        var definition = catalog.GetRequired(catalog.Normalize(sectionType));
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in definition.EntryFields)
        {
            var value = ReadImportValue(field.ImportKey, entry, sectionType);
            if (value is null)
            {
                continue;
            }

            if (value is string stringValue && string.IsNullOrWhiteSpace(stringValue))
            {
                continue;
            }

            if (value is IReadOnlyList<string> listValue && listValue.Count == 0)
            {
                continue;
            }

            fields[field.Id] = value;
        }

        return fields;
    }

    public static Dictionary<string, object?> FromWriteDto(ICvSectionCatalog catalog, string sectionType, CvStructuredEntryWriteDto entry) =>
        FromFlatProjection(catalog, sectionType, entry);

    public static Dictionary<string, object?> FromEntityLegacy(
        ICvSectionCatalog catalog,
        string sectionType,
        string title,
        string? subtitle,
        string? dateRange,
        string summary,
        string bulletsJson,
        string techStack)
    {
        var flat = new CvStructuredEntryWriteDto(
            null,
            title,
            subtitle,
            dateRange,
            summary,
            CvStructuredJson.DeserializeBullets(bulletsJson),
            techStack,
            CvEntrySources.Manual,
            null,
            0);

        return FromFlatProjection(catalog, sectionType, flat);
    }

    public static CvStructuredEntryWriteDto ToWriteDto(
        ICvSectionCatalog catalog,
        string sectionType,
        Dictionary<string, object?> fields,
        CvStructuredEntryWriteDto metadata)
    {
        var flat = ToFlatProjection(catalog, sectionType, fields);

        return new CvStructuredEntryWriteDto(
            metadata.Id,
            flat.Title,
            flat.Subtitle,
            flat.DateRange,
            flat.Summary,
            flat.Bullets,
            flat.TechStack,
            metadata.Source,
            metadata.SourceSummaryId,
            metadata.SortOrder);
    }

    public static CvStructuredEntryDto ToReadDto(
        ICvSectionCatalog catalog,
        string sectionType,
        Guid id,
        string? fieldsJson,
        string title,
        string? subtitle,
        string? dateRange,
        string summary,
        string bulletsJson,
        string techStack,
        string source,
        Guid? sourceSummaryId,
        int sortOrder)
    {
        var fields = !string.IsNullOrWhiteSpace(fieldsJson)
            ? DeserializeFields(fieldsJson)
            : FromEntityLegacy(catalog, sectionType, title, subtitle, dateRange, summary, bulletsJson, techStack);

        var flat = ToFlatProjection(catalog, sectionType, fields);

        return new CvStructuredEntryDto(
            id,
            flat.Title,
            flat.Subtitle,
            flat.DateRange,
            flat.Summary,
            flat.Bullets,
            flat.TechStack,
            fields,
            source,
            sourceSummaryId,
            sortOrder);
    }

    private static Dictionary<string, object?> FromFlatProjection(
        ICvSectionCatalog catalog,
        string sectionType,
        CvStructuredEntryWriteDto entry)
    {
        var normalizedType = catalog.Normalize(sectionType);
        var definition = catalog.GetRequired(normalizedType);
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in definition.EntryFields)
        {
            switch (field.Id)
            {
                case "title" or "groupTitle":
                    fields[field.Id] = entry.Title?.Trim() ?? string.Empty;
                    break;
                case "subtitle":
                    fields[field.Id] = entry.Subtitle?.Trim();
                    break;
                case "dateRange":
                    fields[field.Id] = entry.DateRange?.Trim();
                    break;
                case "summary" or "body":
                    fields[field.Id] = entry.Summary?.Trim() ?? string.Empty;
                    break;
                case "bullets":
                    fields[field.Id] = entry.Bullets?.Where((bullet) => !string.IsNullOrWhiteSpace(bullet)).ToArray() ?? [];
                    break;
                case "techStack":
                    fields[field.Id] = entry.TechStack?.Trim() ?? string.Empty;
                    break;
                case "skills":
                    fields[field.Id] = ParseSkillsList(entry);
                    break;
                case "lines":
                    fields[field.Id] = entry.Bullets?.Where((bullet) => !string.IsNullOrWhiteSpace(bullet)).ToArray() ?? [];
                    break;
            }
        }

        return fields;
    }

    private static FlatProjection ToFlatProjection(
        ICvSectionCatalog catalog,
        string sectionType,
        Dictionary<string, object?> fields)
    {
        var normalizedType = catalog.Normalize(sectionType);

        return normalizedType switch
        {
            CvSectionTypes.Skills => new FlatProjection(
                GetString(fields, "groupTitle"),
                null,
                null,
                string.Empty,
                [],
                string.Join(", ", GetStringList(fields, "skills"))),
            CvSectionTypes.Summary => new FlatProjection(
                string.Empty,
                null,
                null,
                GetString(fields, "body"),
                [],
                string.Empty),
            CvSectionTypes.Contact => new FlatProjection(
                string.Empty,
                null,
                null,
                string.Empty,
                GetStringList(fields, "lines"),
                string.Empty),
            _ => new FlatProjection(
                GetString(fields, "title"),
                GetNullableString(fields, "subtitle"),
                GetNullableString(fields, "dateRange"),
                GetString(fields, "summary") is { Length: > 0 } summaryText
                    ? summaryText
                    : GetString(fields, "body"),
                GetStringList(fields, "bullets"),
                GetString(fields, "techStack"))
        };
    }

    private static object? ReadImportValue(string importKey, CvStructuredImportEntryResult entry, string sectionType)
    {
        return importKey switch
        {
            "title" => entry.Title,
            "subtitle" => entry.Subtitle,
            "dateRange" => entry.DateRange,
            "summary" => entry.Summary,
            "bullets" => entry.Bullets?.Where((bullet) => !string.IsNullOrWhiteSpace(bullet)).ToArray() ?? [],
            "techStack" => entry.TechStack,
            "skills" when catalogSectionIsSkills(sectionType) => ParseSkillsFromImport(entry),
            _ => null
        };

        static bool catalogSectionIsSkills(string sectionType) =>
            string.Equals(sectionType, CvSectionTypes.Skills, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ParseSkillsFromImport(CvStructuredImportEntryResult entry)
    {
        var techStack = entry.TechStack?.Trim();

        if (!string.IsNullOrWhiteSpace(techStack))
        {
            return techStack
                .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where((item) => item.Length > 0)
                .ToArray();
        }

        return entry.Bullets?
            .Select((bullet) => bullet.Trim())
            .Where((bullet) => bullet.Length > 0)
            .ToArray() ?? [];
    }

    private static IReadOnlyList<string> ParseSkillsList(CvStructuredEntryWriteDto entry)
    {
        var techStack = entry.TechStack?.Trim();

        if (!string.IsNullOrWhiteSpace(techStack))
        {
            return techStack
                .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where((item) => item.Length > 0)
                .ToArray();
        }

        return entry.Bullets?
            .Select((bullet) => bullet.Trim())
            .Where((bullet) => bullet.Length > 0)
            .ToArray() ?? [];
    }

    private static string GetString(Dictionary<string, object?> fields, string key)
    {
        if (!fields.TryGetValue(key, out var value) || value is null)
        {
            return string.Empty;
        }

        return value switch
        {
            string text => text,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string? GetNullableString(Dictionary<string, object?> fields, string key)
    {
        var value = GetString(fields, key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IReadOnlyList<string> GetStringList(Dictionary<string, object?> fields, string key)
    {
        if (!fields.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        if (value is IReadOnlyList<string> strings)
        {
            return strings;
        }

        if (value is List<string> list)
        {
            return list;
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Select((item) => item.GetString() ?? string.Empty)
                .Where((item) => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        var text = GetString(fields, key);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where((item) => item.Length > 0)
            .ToArray();
    }

    private sealed record FlatProjection(
        string Title,
        string? Subtitle,
        string? DateRange,
        string Summary,
        IReadOnlyList<string> Bullets,
        string TechStack);
}
