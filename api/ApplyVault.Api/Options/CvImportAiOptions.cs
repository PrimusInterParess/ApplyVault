using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

public sealed class CvImportAiOptions
{
    public const string SectionName = "CvImportAi";

    public const string DefaultSystemPrompt =
        """
        You structure CV/resume text extracted from a PDF into editable sections and entries.
        Return JSON only. Do not wrap in markdown fences.
        Use only facts present in the source text. Do not invent employers, projects, dates, technologies, or achievements.
        Preserve the original order of sections and entries when possible.

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
          - bullets: achievement or skill bullets as plain strings without leading "-" or "*"
          - techStack: comma-separated technologies when relevant; empty string otherwise

        Decisive rules:
        - One entry per job, project, or degree — never merge multiple roles into one entry
        - Put dates only in dateRange, never in title or subtitle
        - Put bullet-like lines in bullets, not in summary
        - For Skills sections, put individual skills in bullets; use title for skill groups only
        - For Summary sections, use a single entry with prose in summary; title may be empty or "Summary"
        - Do not use markdown, HTML, or bold markers in any field
        - Preserve contact details (email, phone, LinkedIn, GitHub, website, location) in a Custom section with heading "Contact"
        - Put contact details in bullets when there are multiple items; keep page numbers out
        - If contact lines appear before summary/profile text, split them into a Contact section and keep the prose in Summary
        - If a raw section mixes experience and projects, split into separate sections with correct sectionType
        - Do not invent facts; improve structure only
        """;

    public const string DefaultUserPromptTemplate =
        """
        Structure the following CV sections extracted from a PDF into JSON.

        Each item in the payload has:
        - heading: section heading from the PDF
        - normalizedKey: detected section category hint (use as guidance, not the final sectionType)
        - text: raw section body text

        Return sections with entries containing title, subtitle, dateRange, summary, bullets, and techStack.
        Apply the field-placement rules from the system prompt decisively.

        Payload:
        {{payloadJson}}
        """;

    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
