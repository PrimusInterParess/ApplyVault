using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

/// <summary>
/// Prompt and gate knobs for optional Gemini structuring during PDF CV import.
/// Orchestration (heuristic-first + when to call AI) lives in CvStructuredImportService;
/// this section only supplies additive options the backend gate reads and prompt text the import client uses.
/// </summary>
public sealed class CvImportAiOptions
{
    public const string SectionName = "CvImportAi";

    /// <summary>
    /// Prefixed to the catalog-generated system prompt at call time.
    /// Frames Gemini as a gated fallback structurer (not always-on).
    /// </summary>
    public const string DefaultSystemPromptPreface =
        """
        You are invoked only when deterministic PDF CV import structuring needs help.
        Improve structure into editable sections and entries. Return JSON only.
        Use only facts present in the source text. Do not invent employers, projects, dates, technologies, or achievements.
        Do not claim or imply that AI assistance was used.
        """;

    /// <summary>
    /// Full system-prompt intent for documentation / optional config override of the preface+rules narrative.
    /// Runtime system instruction is SystemPromptPreface + catalog-generated section/field rules (ADR-0001).
    /// </summary>
    public const string DefaultSystemPrompt =
        """
        You are invoked only when deterministic PDF CV import structuring needs help.
        You structure CV/resume text extracted from a PDF into editable sections and entries.
        Return JSON only. Do not wrap in markdown fences.
        Use only facts present in the source text. Do not invent employers, projects, dates, technologies, or achievements.
        Preserve the original order of sections and entries when possible.
        Do not claim or imply that AI assistance was used.

        sectionType must be one of: Experience, Projects, Education, Skills, Summary, Custom.
        Map headings using these rules:
        - work/professional/employment/career history -> Experience (normalize heading to "Experience" when appropriate)
        - projects/personal projects/side projects -> Projects
        - education/degrees -> Education
        - skills/technical skills/competencies -> Skills
        - summary/profile/about/objective -> Summary
        - contact/contact information -> Custom with heading "Contact"
        - certifications, awards, honors, languages, volunteer, publications, references -> Custom
        - anything else -> Custom

        For every section return:
        - heading: concise section title
        - sectionType: Experience | Projects | Education | Skills | Summary | Custom
        - entries: array of structured items with:
          - title: role, project, degree, or skill group name
          - subtitle: employer, institution, or context (optional)
          - dateRange: plain text dates such as "Jan 2020 – Present" (optional)
          - summary: short prose paragraph(s); use a single string, not markdown
          - bullets: achievement bullets as plain strings without leading "-" or "*"; leave empty for Skills
          - techStack: comma-separated technologies or skills when relevant; empty string otherwise

        Decisive rules:
        - One entry per job, project, or degree — never merge multiple roles into one entry
        - Put dates only in dateRange, never in title or subtitle
        - Put bullet-like lines in bullets, not in summary
        - For Skills sections, put skills in techStack as a comma-separated string; use title for skill groups only; leave bullets empty
        - For Summary sections, use a single entry with prose in summary; title may be empty or "Summary"
        - Do not use markdown, HTML, or bold markers in any field
        - Preserve contact details (email, phone, LinkedIn, GitHub, website, location) in a Custom section with heading "Contact"
        - Contact section must include every email, phone, LinkedIn, GitHub, website, and location line from the header block
        - Put contact details in bullets when there are multiple items; keep page numbers out
        - Choose field placement by sectionType context, but never omit source lines; move uncertain lines to Custom
        - If contact lines appear before summary/profile text, split them into a Contact section and keep the prose in Summary
        - If a raw section mixes experience and projects, split into separate sections with correct sectionType
        - Do not invent facts; improve structure only
        """;

    public const string DefaultUserPromptTemplate =
        """
        Deterministic structuring was insufficient for a confident import.
        Structure the following CV sections extracted from a PDF into JSON.
        Use only facts present in the source text. Do not invent employers, projects, dates, technologies, or achievements.

        Each item in the payload has:
        - heading: section heading from the PDF
        - normalizedKey: detected section category hint (use as guidance, not the final sectionType)
        - text: raw section body text

        Return sections with entries containing title, subtitle, dateRange, summary, bullets, and techStack.
        Apply the field-placement rules from the system prompt decisively.

        Payload:
        {{payloadJson}}
        """;

    /// <summary>
    /// When true (and GoogleAi:Enabled with non-Empty extraction), the import gate should call Gemini
    /// even if heuristic confidence is high. Default false — product path is heuristic-first with gated AI.
    /// </summary>
    public bool ForceAi { get; set; }

    /// <summary>
    /// Gate tuning (read by CvStructuredImportAiGate): minimum combined raw section body characters for the
    /// "only default Profile/Summary bucket + large body" low-confidence signal.
    /// </summary>
    [Range(1, 100_000)]
    public int LowConfidenceMinBodyChars { get; set; } = 400;

    /// <summary>
    /// Optional extract/gate tuning: average extracted characters per page at or below this may be treated as Sparse.
    /// Backend extractor currently uses an in-code constant; this key is additive for config-driven tuning.
    /// Empty extraction remains a hard fail — AI must not invent text.
    /// </summary>
    [Range(1, 10_000)]
    public int SparseMaxAverageCharsPerPage { get; set; } = 120;

    /// <summary>
    /// Prefixed to the ADR-0001 catalog-generated system prompt when calling Gemini.
    /// </summary>
    [Required]
    public string SystemPromptPreface { get; set; } = DefaultSystemPromptPreface;

    /// <summary>
    /// Documented full system-prompt narrative. Not used alone at runtime; catalog rules are appended via the import client.
    /// Kept for config discoverability and parity with other *Ai options sections.
    /// </summary>
    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
