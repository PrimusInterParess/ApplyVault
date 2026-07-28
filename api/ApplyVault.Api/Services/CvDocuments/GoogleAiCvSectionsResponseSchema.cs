using ApplyVault.Api.Services.CvSectionCatalog;

namespace ApplyVault.Api.Services;

internal static class GoogleAiCvSectionsResponseSchema
{
    private static readonly string[] WireImportKeys =
    [
        "title",
        "subtitle",
        "dateRange",
        "summary",
        "bullets",
        "techStack"
    ];

    public static object Create(ICvSectionCatalog? catalog = null)
    {
        var sectionTypeIds = (catalog?.SectionTypes ?? [])
            .Select((type) => type.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy((id) => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "OBJECT",
                required = new[] { "sections" },
                properties = new
                {
                    sections = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            required = new[] { "heading", "sectionType", "entries" },
                            properties = new
                            {
                                heading = new { type = "STRING" },
                                sectionType = BuildSectionTypeSchema(sectionTypeIds),
                                entries = new
                                {
                                    type = "ARRAY",
                                    items = new
                                    {
                                        type = "OBJECT",
                                        required = new[] { "title", "summary", "bullets", "techStack" },
                                        properties = BuildEntryWireProperties()
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static Dictionary<string, object> BuildEntryWireProperties()
    {
        var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in WireImportKeys)
        {
            properties[key] = key switch
            {
                "bullets" => new
                {
                    type = "ARRAY",
                    items = new { type = "STRING" }
                },
                _ => new { type = "STRING" }
            };
        }

        return properties;
    }

    private static object BuildSectionTypeSchema(IReadOnlyList<string> sectionTypeIds) =>
        sectionTypeIds.Count > 0
            ? new { type = "STRING", @enum = sectionTypeIds }
            : (object)new { type = "STRING" };
}
