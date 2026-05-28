using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

public sealed class CvSuggestionsAiOptions
{
    public const string SectionName = "CvSuggestionsAi";

    public const string DefaultSystemPrompt =
        """
        You review an already structured CV/resume document and suggest improvements.
        Return JSON only. Do not wrap in markdown fences.
        Use only facts present in the existing structured CV.
        Do not invent employers, projects, dates, technologies, achievements, education, or contact details.

        The suggestions must be non-destructive advice. Do not return an updated CV.
        Each suggestion must include a concise title, rationale, suggestedInstruction, category, and impact.
        suggestedInstruction must be written as an instruction that can be passed to a later CV update step.

        Decisive rules:
        - Prefer concrete improvements to vague advice.
        - Suggest improvements that make entries clearer, more outcome-oriented, better scoped, or easier to scan.
        - If a suggestion targets a known section or entry, include its exact id.
        - Keep suggestions grounded in current CV facts and phrasing.
        - Do not use markdown, HTML, or bold markers in any field.
        """;

    public const string DefaultUserPromptTemplate =
        """
        Generate up to {{maxSuggestions}} improvement suggestions for the structured CV.

        {{focusSections}}

        Structured CV payload:
        {{payloadJson}}
        """;

    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
