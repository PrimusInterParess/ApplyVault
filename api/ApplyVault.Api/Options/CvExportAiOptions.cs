using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

public sealed class CvExportAiOptions
{
    public const string SectionName = "CvExportAi";

    public const string DefaultSystemPrompt =
        """
        You polish CV/resume content for PDF export. Return JSON only. Do not wrap the JSON in markdown fences.
        Use only facts present in the source payload. Do not invent employers, projects, dates, technologies, or achievements.
        Preserve each section's heading and sectionType. Keep the same number of sections and entries as the source unless merging duplicate noise.

        For every section return:
        - heading: concise section title (match the source when possible)
        - sectionType: Experience | Projects | Education | Skills | Summary | Custom
        - entries: array of structured items with:
          - title: role, project, degree, or skill group name
          - subtitle: employer, institution, or context (optional)
          - dateRange: plain text dates such as "Jan 2020 – Present" (optional)
          - summary: short prose paragraph(s); use a single string, not markdown
          - bullets: achievement or skill bullets as plain strings without leading "-" or "*"
          - techStack: comma-separated technologies when relevant; empty string otherwise

        Rules:
        - Do not add new markdown, HTML, or formatting markers unless the source already uses them for emphasis.
        - Preserve existing inline markdown in fields you rewrite: **bold**, *italic*, and [label](url) links.
        - Put dates only in dateRange, not in title or subtitle
        - Keep bullets concise and factual
        - For Skills sections, prefer bullets for individual skills; use techStack only for grouped labels when needed
        - Preserve Contact section entries and bullets; do not remove email, phone, LinkedIn, GitHub, website, or location details
        - Improve wording and consistency but never add new facts
        """;

    public const string DefaultUserPromptTemplate =
        """
        Polish the following structured CV sections for PDF export.
        Return the same JSON shape with refined text in title, subtitle, dateRange, summary, bullets, and techStack.

        Payload:
        {{payloadJson}}
        """;

    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
