using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

/// <summary>
/// Prompt knobs for Gemini structuring during PDF CV import (AI-first when GoogleAi:Enabled).
/// Pipeline: Extract full text → this prompt fills Structured CV JSON → light normalize.
/// </summary>
public sealed class CvImportAiOptions
{
    public const string SectionName = "CvImportAi";

    /// <summary>
    /// Prefixed to the catalog-generated system prompt at call time.
    /// </summary>
    public const string DefaultSystemPromptPreface =
        """
        You structure CV/resume text extracted from a PDF into editable sections and entries.
        Return JSON only. Use only facts present in the source text. Do not invent content.
        Do not claim or imply that AI assistance was used.

        CONTACT IS MANDATORY when the source has a header block (name / email / phone / address / LinkedIn / GitHub).
        Emit a section with sectionType Contact and heading "Contact". Never put contact details only inside Summary.
        Never emit Custom with heading "Contact".

        Contact entries (one channel per entry; value in bullets unless Name):
        - title "Name", subtitle = person's full name (required when a name appears at the top of the CV)
        - title "Email", bullets: ["email@domain"]
        - title "Phone", bullets: ["+45 …"]
        - title "Address" or "Location", bullets: ["full street, postal city, country"] — keep commas; do not split the address
        - title "LinkedIn", bullets: ["www.linkedin.com/in/…"] — full URL/path as one token
        - title "GitHub", bullets: ["github.com/…"] — full URL/path as one token
        - title "Website" for other sites

        Keep URLs and emails as single atomic tokens; never split on "/".
        Park anything else that does not fit a typed section in Custom (heading "Additional information" when needed).
        """;

    /// <summary>
    /// Documented full system-prompt narrative. Runtime uses SystemPromptPreface + catalog rules.
    /// </summary>
    public const string DefaultSystemPrompt =
        """
        You structure CV/resume text extracted from a PDF into editable sections and entries.
        Return JSON only. Do not wrap in markdown fences.
        Use only facts present in the source text. Do not invent employers, projects, dates, technologies, or achievements.
        Preserve the original order of sections and entries when possible.
        Do not claim or imply that AI assistance was used.

        sectionType must be one of: Experience, Projects, Education, Skills, Summary, Contact, Custom.
        Map headings using these rules:
        - work/professional/employment/career history -> Experience
        - projects/personal projects/side projects -> Projects
        - education/degrees -> Education
        - skills/technical skills/competencies -> Skills
        - summary/profile/about/objective -> Summary (prose only — not email/phone/links/name)
        - contact/contact information -> Contact
        - certifications, awards, honors, languages, volunteer, publications, references -> Custom
        - anything else -> Custom

        For every section return heading, sectionType, and entries with:
        title, subtitle, dateRange, summary, bullets, techStack.

        Decisive rules:
        - One entry per job, project, or degree
        - Put dates only in dateRange
        - Skills: techStack comma-separated; title for skill groups; bullets empty
        - Summary: single entry with prose in summary only
        - Contact: REQUIRED separate section whenever name/email/phone/address/LinkedIn/GitHub appear in the source header
        - Contact Name: title exactly "Name", subtitle = full name
        - Contact channels: separate entries titled Email, Phone, Address (or Location), LinkedIn, GitHub, Website — each value in bullets[0]
        - Never bury Contact fields inside Summary.summary
        - Never split URLs or street addresses on "/" or commas
        - Never omit source lines; use Custom "Additional information" for leftovers
        - Do not invent facts; improve structure only
        """;

    /// <summary>
    /// User message template. Replace <c>{{payload}}</c> with the full ordered extracted CV text.
    /// </summary>
    public const string DefaultUserPromptTemplate =
        """
        Structure the following extracted CV text into JSON sections and entries.
        Use only facts from the source.

        First, extract Contact from the header (name, address, phone, email, LinkedIn, GitHub) into sectionType Contact
        with separate entries (Name subtitle + Email/Phone/Address/LinkedIn/GitHub bullets). Do not leave Contact empty
        if those lines exist. Do not put them only in Summary.

        Then structure Experience, Projects, Education, Skills, Summary, and Custom as needed.
        Keep URLs and full addresses as single values.

        Extracted CV text:
        {{payload}}
        """;

    [Range(1, 10_000)]
    public int SparseMaxAverageCharsPerPage { get; set; } = 120;

    [Required]
    public string SystemPromptPreface { get; set; } = DefaultSystemPromptPreface;

    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
