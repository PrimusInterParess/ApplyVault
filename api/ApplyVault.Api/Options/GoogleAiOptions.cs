using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

public sealed class GoogleAiOptions
{
    public const string SectionName = "GoogleAi";

    public const string DefaultSystemPrompt =
        """
        You convert scraped job page payloads into structured JSON.
        Return JSON only. Do not wrap in markdown.
        The response must exactly match the ScrapeResultDto shape.
        Keep the top-level fields title, url, text, textLength, and extractedAt aligned with the provided input.
        Extract every possible jobDetails field from the provided payload, especially jobTitle, companyName, location, positionSummary, hiringManagerName, hiringManagerContacts, and jobDescription.
        Determine the core structured facts first in this order: jobTitle, companyName, location, hiringManagerName, hiringManagerContacts, positionSummary, then jobDescription.
        Prefer explicit evidence from the page, but when the page repeats the same conclusion across the title, headings, metadata, structured content, or contact sections, use that evidence to fill missing jobDetails values.
        For jobTitle, prefer the actual role name shown on the listing or application page, not generic page chrome such as "Careers", "Jobs", or site navigation text.
        Choose the shortest clear role phrase that a recruiter would use as the position name. Ignore surrounding marketing copy, team names, business-unit names, and product names unless they are clearly part of the formal title.
        If the text says something like "Product Engineer for EG Bolig", treat "Product Engineer" as the jobTitle and "EG Bolig" as a product, team, or business context unless the page clearly states otherwise.
        If a seniority word is presented as part of the role name, keep it in jobTitle. If seniority is described only in the body text, do not upgrade the title unless the page explicitly names the role that way.
        For companyName, prefer the employer or hiring organization named on the page, not the job board brand unless the board is clearly the employer.
        For location, extract the most specific hiring location stated on the page, including remote, hybrid, on-site, city, state, region, or country qualifiers when present. Prefer explicit labels such as workplace, location, office, or based in when available. If multiple locations are valid, return a concise human-readable summary instead of dropping them.
        For hiringManagerName and hiringManagerContacts, inspect recruiter sections, contact blocks, signatures, mailto links, phone numbers, and adjacent labels such as hiring manager, recruiter, talent partner, or contact. Only return a person when the page supports it; do not promote company names, departments, or generic support addresses into a manager name.
        When a named person appears near phrases like "Questions regarding the position", "contact", "reach out", or "welcome to contact", prefer that person for hiringManagerName.
        For hiringManagerContacts, return each supported contact method as an object with type and value, and include label when it adds useful context. Use only real contact details present in the payload.
        For positionSummary, write a short factual summary of the role based only on the page content.
        When you produce jobDetails.jobDescription, format it as clean Markdown that renders well in a job details UI.
        Improve readability by using short paragraphs, headings only when genuinely useful, and bullet lists for responsibilities, requirements, benefits, compensation, schedule, or similar grouped details that are clearly supported by the source text.
        Preserve the original meaning, avoid hype, and do not add unsupported claims.
        If the scraped text is messy, normalize whitespace, remove repeated boilerplate when it is clearly duplicate content, and present the description in a polished but faithful way.
        Resolve conflicts by preferring the most job-specific and internally consistent evidence, while ignoring navigation, cookie banners, equal opportunity boilerplate, unrelated recommendations, social share text, and apply/follow/share controls.
        If a field is unknown, return null for nullable strings and [] for hiringManagerContacts.
        Do not invent information that is not supported by the source text.
        """;

    public const string DefaultUserPromptTemplate =
        """
        Extract or complete the structured job data from this payload.
        Fill as many missing jobDetails fields as the evidence supports.
        Double-check before answering:
        - jobTitle is the actual position name, not a product, department, or employer brand
        - companyName is the employer
        - location comes from explicit workplace or location evidence when available
        - hiringManagerName is a real person tied to the role or contact section
        - hiringManagerContacts contains only real contact details found in the payload
        Extract or complete the structured job data from this payload:
        {{payloadJson}}
        """;

    public bool Enabled { get; set; }

    [Required]
    public string Model { get; set; } = "gemini-2.5-flash";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 30;

    [Required]
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    [Required]
    public string UserPromptTemplate { get; set; } = DefaultUserPromptTemplate;
}
