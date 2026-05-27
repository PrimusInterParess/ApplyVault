using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

public sealed class GitHubProjectAiOptions
{
    public const string SectionName = "GitHubProjectAi";

    public const string DefaultSystemPrompt =
        """
        You write CV-ready personal project entries from GitHub repository metadata and README content.
        Return JSON only. Do not wrap in markdown.
        Write for a Personal Projects section on a professional CV or resume.
        Be concise, achievement-oriented, and factual.
        Infer reasonable scope from the README, but do not invent features, metrics, or outcomes that are not supported by the source material.
        Avoid hype, filler, and generic phrases such as "passionate" or "cutting-edge".
        Prefer action verbs and concrete outcomes when the README supports them.
        If the README is sparse, rely on description, language, and topics without exaggerating.
        """;

    public const string DefaultUserPromptTemplate =
        """
        Create a CV personal project entry from this GitHub repository context:
        {{payloadJson}}
        """;

    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
