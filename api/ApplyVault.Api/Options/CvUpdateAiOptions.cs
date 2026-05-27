using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

public sealed class CvUpdateAiOptions
{
    public const string SectionName = "CvUpdateAi";

    public const string DefaultSystemPrompt =
        """
        You update an already structured CV/resume document from user instructions.
        Return JSON only. Do not wrap in markdown fences.
        Use only facts present in the existing structured CV or explicitly provided by the user's instructions.
        Do not invent employers, projects, dates, technologies, achievements, education, or contact details.

        The response must contain the full updated CV, not a patch.
        Preserve section and entry ids when the item still represents the same real-world section or entry.
        Use null or omit id only for newly created sections or entries.
        Preserve sortOrder values when order is unchanged; otherwise return dense zero-based sortOrder values.
        Preserve source and sourceSummaryId for unchanged entries when present.
        Use source "Manual" for entries that are newly created or materially rewritten by the instruction.

        sectionType must be one of: Experience, Projects, Education, Skills, Summary, Custom.
        Each section must include heading, sectionType, sortOrder, and entries.
        Each entry must include title, subtitle, dateRange, summary, bullets, techStack, source, sourceSummaryId, and sortOrder.

        Decisive rules:
        - Apply the user's instructions to the whole CV, not just one section.
        - Keep unchanged content unchanged unless needed to satisfy the instruction.
        - Put dates only in dateRange, never in title or subtitle.
        - Put bullet-like achievements in bullets, not in summary.
        - For Skills sections, put individual skills in bullets; use title for skill groups only.
        - For Summary sections, use a single entry with prose in summary; title may be empty or "Summary".
        - Do not use markdown, HTML, or bold markers in any field.
        - If an instruction conflicts with the current CV facts and does not provide replacement facts, preserve the current facts.
        """;

    public const string DefaultUserPromptTemplate =
        """
        Update the structured CV according to the instructions.

        Instructions:
        {{instructions}}

        Structured CV payload:
        {{payloadJson}}
        """;

    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
