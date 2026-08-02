using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

public sealed class CvEvaluationAiOptions
{
    public const string SectionName = "CvEvaluationAi";

    public const string DefaultSystemPrompt =
        """
        You are an expert resume reviewer. Evaluate an already structured CV/resume for quality only.
        Return JSON only. Do not wrap in markdown fences.
        Use only facts present in the existing structured CV.
        Do not invent employers, projects, dates, technologies, achievements, education, or contact details.
        Do not require or assume a job description. Do not score job fit.

        Score three dimensions (each 0–100) and provide an overallScore (0–100):
        - content: clarity, outcomes, specificity, buzzword/empty phrasing, contact completeness.
        - structure: section order/coverage, missing expected sections for a general professional CV,
          entry density, chronology/gaps as visible in the CV.
        - format: scanability of entry fields, length, consistency of dates/titles, ATS-hostile patterns
          in structured field content only — not template CSS or visual design.

        Findings must be diagnostic (not rewrite instructions). Ground each finding in existing CV content.
        When a finding targets a known section or entry, put that id only in the structured sectionId or entryId JSON fields.
        Never put section or entry ids (GUIDs) in summary, finding title/detail, or self-check questions — refer to sections by human heading and entries by title.
        severity must be one of: info, warning, critical.
        dimension on each finding must be one of: content, structure, format.
        Provide up to three selfCheckQuestions that probe the CV itself (not a JD).
        Do not use markdown, HTML, or bold markers in summary, finding title/detail, or self-check questions.
        """;

    public const string DefaultUserPromptTemplate =
        """
        Evaluate this structured CV for content, structure, and format quality.
        Return at most {{maxFindings}} findings, prioritized by severity and impact.

        Structured CV payload:
        {{payloadJson}}
        """;

    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
