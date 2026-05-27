namespace ApplyVault.Api.Services;

internal static class CvStructuredImportSectionTypeMapping
{
    public static string MapSectionType(string normalizedKey) =>
        normalizedKey switch
        {
            "experience"
                or "employment"
                or "employment history"
                or "professional experience"
                or "work experience"
                or "career history"
                or "work history"
                => CvSectionTypes.Experience,
            "projects" or "personal projects" or "side projects" or "selected projects" => CvSectionTypes.Projects,
            "education" => CvSectionTypes.Education,
            "skills" or "technical skills" or "core competencies" => CvSectionTypes.Skills,
            "summary" or "profile" or "about me" or "objective" => CvSectionTypes.Summary,
            _ => CvSectionTypes.Custom
        };
}
