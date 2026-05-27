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
        Do not invent features, metrics, integrations, or outcomes that are not supported by the source material.
        Avoid hype, filler, and generic phrases such as "passionate" or "cutting-edge".

        Before writing anything, decide whether there is enough factual context to produce a credible CV entry.
        If there is not enough context, return sufficientContext=false, leave title, summary, bullets, and techStack as empty strings, and explain briefly in refusalReason what is missing.
        Treat context as insufficient when all meaningful sources are missing or empty, including README, repository description, primary language, and topics.
        Primary language alone is never enough. A short placeholder description plus a language is not enough.
        Also treat context as insufficient when the only available text is placeholder-level content such as a bare repository name, "test", "testing", "todo", "Testing arts", or a single generic phrase with no project scope.
        Example insufficient context: name "Common", description "Testing arts", primaryLanguage "C#", no README, no topics. Return sufficientContext=false.
        Example sufficient context: README with features and setup details, or description "Built a full-stack job application tracker with scraping, auth, and calendar sync."
        When sufficientContext=true, return a complete CV entry and set refusalReason to an empty string.
        Never fabricate project purpose, features, impact, or technologies that are not supported by the provided context.

        When sufficientContext=true, extract the most CV-worthy facts in this order:
        1. Project purpose and problem domain — from the README intro, description, or overview-style sections.
        2. What was built — named features, workflows, APIs, UIs, integrations, data flows, automations, or deliverables.
        3. Technical approach — architecture, patterns, frameworks, services, or infrastructure only when explicitly mentioned.
        4. Outcomes or scale — user counts, performance, metrics, or impact only if stated in the source.

        When scanning readmeText, prioritize sections whose headings or lead text indicate features, capabilities, overview, about, architecture, tech stack, stack, built with, technologies, implementation, or highlights.
        Deprioritize install/run commands, directory trees, contribution guidelines, license text, badge rows, screenshots, and generic template boilerplate unless they name real project capabilities.
        If the README is sparse, rely on description, primaryLanguage, and topics without exaggerating.

        Field rules when sufficientContext=true:
        - title: A concise, human-readable project name for a CV. Prefer what the project does over the raw repo slug when the source supports it.
        - summary: One or two sentences stating what was built and the main technical or domain context. Target roughly 140-280 characters when the source supports it. Do not list every feature here.
        - bullets: Return 2 to 4 bullets when the source supports distinct capabilities. Each bullet must add CV-worthy detail that is not already fully covered by summary. Lead with strong verbs such as Built, Implemented, Integrated, or Designed. One concrete capability, integration, or technical decision per bullet.
        - techStack: Comma-separated technologies clearly evidenced in the README, description, topics, or primaryLanguage. Include frameworks, databases, cloud services, and major libraries when named. Omit vague labels such as "web" or "app" unless they are the only supported signal.
        """;

    public const string DefaultUserPromptTemplate =
        """
        Review the GitHub repository context below and produce a CV personal project entry.

        Before answering, extract from the payload:
        - The problem or domain this project addresses
        - The main features or capabilities worth highlighting on a CV
        - Technologies explicitly named in the README, description, topics, or primaryLanguage
        - Any stated outcomes or constraints (only if present in the source)

        If there is not enough factual context, return sufficientContext=false and explain briefly in refusalReason.
        If there is enough context, return sufficientContext=true with title, summary, 2-4 distinct bullets when supported, and techStack.

        Double-check before returning sufficientContext=true:
        - summary captures purpose and scope, not install steps or repo boilerplate
        - each bullet is a distinct capability or technical choice supported by the source
        - bullets do not repeat the summary verbatim
        - techStack lists only technologies evidenced in the payload

        Repository context:
        {{payloadJson}}
        """;

    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
