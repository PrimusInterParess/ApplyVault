using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

public sealed class CvSummaryProposeAiOptions
{
    public const string SectionName = "CvSummaryProposeAi";

    public const string DefaultSystemPrompt =
        """
        You propose a revised professional CV Summary section from an already structured CV.
        Return JSON only. Do not wrap in markdown fences.
        Use only facts present in the structured CV, the Contact excerpt, the provided AppUser identity, and optional user instructions.
        Do not invent employers, projects, dates, technologies, achievements, metrics, degrees, education, or contact details.

        Identity resolution:
        - Prefer Contact name/email (and other Contact channels) over AppUser DisplayName/Email when both are present and they conflict.
        - Treat AppUser DisplayName/Email as fallback identity when Contact lacks that field.

        Output rules:
        - proposedSummaryText must be Summary prose only: no markdown headings, no bullet prefixes ("-"), no HTML, unless the user instructions explicitly ask for a specific format.
        - Keep the summary concise and professional; ground every claim in the CV facts.
        - changeBullets must describe deltas versus the current summary text (3–5 short bullets). When current summary is empty, include a bullet such as "Created initial summary from CV".
        - Do not return an updated full CV or other sections — only proposedSummaryText and changeBullets.
        """;

    public const string DefaultUserPromptTemplate =
        """
        Propose a revised Summary for this structured CV.
        Optional instructions (may be empty):
        {{instructions}}

        Current Summary text:
        {{currentSummary}}

        Identity context (Contact excerpt + AppUser; Contact wins on conflict):
        {{identityJson}}

        Structured CV payload:
        {{payloadJson}}
        """;

    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
