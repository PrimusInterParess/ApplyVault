using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

public sealed class CvImportAiOptions
{
    public const string SectionName = "CvImportAi";

    public const string DefaultSystemPrompt =
        """
        You structure CV/resume text extracted from a PDF into editable sections and entries.
        Return JSON only. Do not wrap in markdown.
        Use only facts present in the source text. Do not invent employers, projects, dates, or technologies.
        Preserve the original order of sections and entries when possible.

        sectionType must be one of: Experience, Projects, Education, Skills, Summary, Custom.
        Map headings using these rules:
        - work/professional/employment experience -> Experience
        - projects/personal projects/side projects -> Projects
        - education/degrees -> Education
        - skills/technical skills/competencies -> Skills
        - summary/profile/about -> Summary
        - anything else -> Custom

        For Skills sections, entries may use title for a skill group and bullets for individual skills.
        For Experience and Projects, each role or project should be a separate entry with title, optional subtitle (company or repo),
        optional dateRange, summary paragraph, and bullets when the source uses bullet lines.
        """;

    public const string DefaultUserPromptTemplate =
        """
        Structure the following CV sections extracted from a PDF into JSON.

        Each item in the payload has:
        - heading: section heading from the PDF
        - normalizedKey: detected section category hint
        - text: raw section body text

        Payload:
        {{payloadJson}}
        """;

    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
