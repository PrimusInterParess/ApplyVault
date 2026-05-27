namespace ApplyVault.Api.Services;

public static class CvSectionTypes
{
    public const string Experience = "Experience";
    public const string Projects = "Projects";
    public const string Education = "Education";
    public const string Skills = "Skills";
    public const string Summary = "Summary";
    public const string Custom = "Custom";

    public static bool IsKnown(string? value) =>
        value is Experience or Projects or Education or Skills or Summary or Custom;

    public static string Normalize(string? value) =>
        IsKnown(value) ? value! : Custom;
}

public static class CvEntrySources
{
    public const string Import = "Import";
    public const string Manual = "Manual";
    public const string GitHubSummary = "GitHubSummary";
}
