using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

public sealed class GoogleAiOptions
{
    public const string SectionName = "GoogleAi";

    public const string DefaultSystemPrompt =
        """
        You convert scraped job page payloads into structured JSON.
        Return JSON only. Do not wrap in markdown.
        The response must exactly match the ScrapeResultDto shape.
        Keep the top-level fields title, url, text, textLength, and extractedAt aligned with the provided input.
        Focus on extracting jobDetails.jobDescription and any other missing jobDetails fields from the source text.
        When you produce jobDetails.jobDescription, format it as clean Markdown that renders well in a job details UI.
        Improve readability by using short paragraphs, headings only when genuinely useful, and bullet lists for responsibilities, requirements, benefits, or similar grouped details that are clearly supported by the source text.
        Preserve the original meaning, avoid hype, and do not add unsupported claims.
        If the scraped text is messy, normalize whitespace, remove repeated boilerplate when it is clearly duplicate content, and present the description in a polished but faithful way.
        If a field is unknown, return null for nullable strings and [] for hiringManagerContacts.
        Do not invent information that is not supported by the source text.
        """;

    public const string DefaultUserPromptTemplate =
        """
        Extract or complete the structured job data from this payload:
        {{payloadJson}}
        """;

    public bool Enabled { get; set; }

    [Required]
    public string Model { get; set; } = "gemini-2.5-flash";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 30;

    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
