using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApplyVault.Api.Services.JobRequirements;

internal static class JobRequirementsJson
{
    public const int SchemaVersion = 1;
    public const int MaxFacts = 120;
    public const int MaxValueCharacters = 500;
    public const int MaxEvidenceCharacters = 1500;
    public const int MaxDocumentCharacters = 250_000;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 16,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static JobRequirementsDocument Parse(string json, string description)
    {
        if (json.Length > MaxDocumentCharacters)
        {
            throw new JsonException("Requirements document exceeds its size limit.");
        }

        var document = JsonSerializer.Deserialize<JobRequirementsDocument>(json, Options)
            ?? throw new JsonException("Missing requirements document.");
        if (document.SchemaVersion != SchemaVersion || document.Facts.Count > MaxFacts)
        {
            throw new JsonException("Unsupported requirements version or fact count.");
        }

        var source = JobRequirementsSource.NormalizeWhitespace(description);
        foreach (var fact in document.Facts)
        {
            if (fact is null || !Enum.IsDefined(fact.Category) || !Enum.IsDefined(fact.Importance) ||
                string.IsNullOrWhiteSpace(fact.Value) || fact.Value.Length > MaxValueCharacters ||
                string.IsNullOrWhiteSpace(fact.Evidence) || fact.Evidence.Length > MaxEvidenceCharacters ||
                !source.Contains(JobRequirementsSource.NormalizeWhitespace(fact.Evidence), StringComparison.Ordinal) ||
                !JobRequirementsSource.NormalizeWhitespace(fact.Evidence).Contains(
                    JobRequirementsSource.NormalizeWhitespace(fact.Value), StringComparison.Ordinal))
            {
                throw new JsonException("Requirements fact is invalid or lacks supporting source evidence.");
            }
        }

        return document with { Facts = Array.AsReadOnly(document.Facts.ToArray()) };
    }

    public static string Serialize(JobRequirementsDocument document) => JsonSerializer.Serialize(document, Options);

    public static object ResponseSchema() => new
    {
        type = "OBJECT",
        required = new[] { "schemaVersion", "facts" },
        properties = new
        {
            schemaVersion = new { type = "INTEGER" },
            facts = new
            {
                type = "ARRAY",
                // Enforce MaxFacts after parsing; a large array bound makes Gemini's grammar too complex.
                items = new
                {
                    type = "OBJECT",
                    required = new[] { "category", "value", "importance", "evidence" },
                    properties = new
                    {
                        category = new { type = "STRING", @enum = JsonSerializer.SerializeToElement(Enum.GetValues<JobRequirementCategory>(), Options) },
                        value = new { type = "STRING" },
                        importance = new { type = "STRING", @enum = JsonSerializer.SerializeToElement(Enum.GetValues<RequirementImportance>(), Options) },
                        evidence = new { type = "STRING" }
                    }
                }
            }
        }
    };
}
