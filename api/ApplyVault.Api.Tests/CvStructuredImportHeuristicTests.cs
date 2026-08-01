using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredImportHeuristicTests
{
    [Fact]
    public void Parse_SummarySection_UsesSingleSummaryEntry()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection("Profile", "summary", 0, "Experienced software engineer focused on backend systems.")
        ]);

        var summary = Assert.Single(sections);

        Assert.Equal(CvSectionTypes.Summary, summary.SectionType);

        var entry = Assert.Single(summary.Entries);

        Assert.Equal("Experienced software engineer focused on backend systems.", entry.Summary);
        Assert.Empty(entry.Bullets);
    }

    [Fact]
    public void Parse_ExperienceSection_SplitsSingleNewlineJobBlock()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Experience",
                "experience",
                0,
                """
                Software Engineer
                Acme Corp
                2020 – 2024
                Built reliable services.
                """)
        ]);

        var experience = Assert.Single(sections);
        var entry = Assert.Single(experience.Entries);

        Assert.Equal("Software Engineer", entry.Title);
        Assert.Equal("Acme Corp", entry.Subtitle);
        Assert.Equal("2020 – 2024", entry.DateRange);
        Assert.Equal("Built reliable services.", entry.Summary);
    }

    [Fact]
    public void Parse_ExperienceSection_SplitsMultipleJobsByDateBoundaries()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Experience",
                "experience",
                0,
                """
                Software Engineer
                Acme Corp
                2020 – 2024
                Built reliable services.
                Senior Engineer
                Beta Inc
                2024 – Present
                Led platform migration.
                """)
        ]);

        var experience = Assert.Single(sections);

        Assert.Equal(2, experience.Entries.Count);
        Assert.Equal("Software Engineer", experience.Entries[0].Title);
        Assert.Equal("Senior Engineer", experience.Entries[1].Title);
        Assert.Equal("2024 – Present", experience.Entries[1].DateRange);
    }

    [Fact]
    public void Parse_SkillsSection_SupportsGroupedSkillLines()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Skills",
                "skills",
                0,
                """
                Languages: English, Danish
                Frameworks: .NET, Angular
                """)
        ]);

        var skills = Assert.Single(sections);

        Assert.Equal(CvSectionTypes.Skills, skills.SectionType);
        Assert.Equal(2, skills.Entries.Count);
        Assert.Equal("Languages", skills.Entries[0].Title);
        Assert.Equal("English, Danish", skills.Entries[0].TechStack);
        Assert.Equal("Frameworks", skills.Entries[1].Title);
        Assert.Equal(".NET, Angular", skills.Entries[1].TechStack);
    }

    [Fact]
    public void Parse_ExperienceSection_ExtractsBulletLines()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Experience",
                "experience",
                0,
                """
                Software Engineer
                Acme Corp
                2020 – 2024
                - Built reliable services.
                - Improved uptime.
                """)
        ]);

        var entry = Assert.Single(Assert.Single(sections).Entries);

        Assert.Equal(["Built reliable services.", "Improved uptime."], entry.Bullets);
    }

    [Fact]
    public void LooksLikeDateLine_RejectsProseWithHyphenAndDigit()
    {
        // Regression: "full-stack" + "V3" previously matched hyphen+digit and filled DateRange,
        // which then exceeded nvarchar(128) on UserCvEntries.DateRange.
        var prose =
            "A full-stack job tracking ecosystem featuring a Chrome Manifest V3 extension, an ASP.NET Core API, and a web app.";

        Assert.False(CvStructuredImportHeuristic.LooksLikeDateLine(prose));
        Assert.True(CvStructuredImportHeuristic.LooksLikeDateLine("2020 – 2024"));
        Assert.True(CvStructuredImportHeuristic.LooksLikeDateLine("2024 – Present"));
        Assert.True(CvStructuredImportHeuristic.LooksLikeDateLine("Jan 2020 – Dec 2024"));
    }

    [Fact]
    public void Parse_ProjectsSection_KeepsDescriptionInSummaryNotDateRange()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Projects",
                "projects",
                0,
                """
                ApplyVault
                A full-stack job tracking ecosystem featuring a Chrome Manifest V3 extension, an ASP.NET Core API, and a web app.
                """)
        ]);

        var entry = Assert.Single(Assert.Single(sections).Entries);

        Assert.Equal("ApplyVault", entry.Title);
        Assert.Null(entry.DateRange);
        Assert.Null(entry.Subtitle);
        Assert.Contains("full-stack job tracking ecosystem", entry.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ExperienceSection_KeepsMultiLineJobsTogetherWithPipeTitles()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Experience",
                "experience",
                0,
                """
                Full-Stack Software Developer | LifeBonder, Vejle, Denmark
                Apr 2026 – Present
                Full-stack role focused on scaling the Admin Portal and stabilizing core infrastructure.
                Cross-Team Orchestration: Bridge the gap between ML, Backend, and Frontend teams.
                Feature Engineering: Design and implement new core admin functionalities.
                Software Developer | Object Systems International, Sofia (TheraPro), Bulgaria
                Jun 2023 – Present
                Full-stack role dedicated to architecting high-performance cloud AI integrations.
                Real-Time Data Architecting: Designed and implemented real-time audio streaming pipelines.
                Purchasing Assistant | Anglo-American School of Sofia, Bulgaria
                Mar 2008 - Jun 2023
                Managed vendor operations and multi-million-dollar budgets in an international institution.
                """)
        ]);

        var experience = Assert.Single(sections);

        Assert.Equal(3, experience.Entries.Count);
        Assert.Equal("Full-Stack Software Developer", experience.Entries[0].Title);
        Assert.Equal("LifeBonder, Vejle, Denmark", experience.Entries[0].Subtitle);
        Assert.Equal("Apr 2026 – Present", experience.Entries[0].DateRange);
        Assert.Contains("Cross-Team Orchestration", experience.Entries[0].Summary, StringComparison.Ordinal);
        Assert.Equal("Software Developer", experience.Entries[1].Title);
        Assert.Equal("Jun 2023 – Present", experience.Entries[1].DateRange);
        Assert.Equal("Purchasing Assistant", experience.Entries[2].Title);
        Assert.Equal("Mar 2008 - Jun 2023", experience.Entries[2].DateRange);
    }

    [Fact]
    public void Parse_ProjectsSection_DoesNotSplitOnTechnologiesOrLinks()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Projects",
                "projects",
                0,
                """
                ApplyVault
                A full-stack job tracking ecosystem featuring a Chrome Manifest V3 extension.
                https://github.com/PrimusInterParess/ApplyVault
                Technologies: C#, ASP.NET Core, Angular
                Translator
                Chrome/Edge browser extension and local ASP.NET Core proxy for translation.
                Technologies: JavaScript, ASP.NET Core, C#
                RaceCorp
                Web application for sharing mountain bike rides and races.
                Technologies: .NET Core 6.0, SignalR
                """)
        ]);

        var projects = Assert.Single(sections);

        Assert.Equal(3, projects.Entries.Count);
        Assert.Equal("ApplyVault", projects.Entries[0].Title);
        Assert.Contains("full-stack job tracking ecosystem", projects.Entries[0].Summary, StringComparison.Ordinal);
        Assert.Contains("github.com", projects.Entries[0].Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("C#, ASP.NET Core, Angular", projects.Entries[0].TechStack);
        Assert.Equal("Translator", projects.Entries[1].Title);
        Assert.Equal("RaceCorp", projects.Entries[2].Title);
    }

    [Fact]
    public void Parse_SkillsSection_SupportsTwoLineGroups()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Skills",
                "skills",
                0,
                """
                Backend
                C#, ASP.NET Core, RESTful APIs, SignalR, EF Core
                Frontend
                Angular (v14+), TypeScript, RxJS
                """)
        ]);

        var skills = Assert.Single(sections);

        Assert.Equal(2, skills.Entries.Count);
        Assert.Equal("Backend", skills.Entries[0].Title);
        Assert.Equal("C#, ASP.NET Core, RESTful APIs, SignalR, EF Core", skills.Entries[0].TechStack);
        Assert.Equal("Frontend", skills.Entries[1].Title);
        Assert.Equal("Angular (v14+), TypeScript, RxJS", skills.Entries[1].TechStack);
    }

    [Fact]
    public void Parse_SummarySection_KeepsRoleHeadlineWhenNoContactPresent()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Summary",
                "summary",
                0,
                """
                Full-Stack Software Developer
                Focused on healthcare and social platforms.
                """)
        ]);

        var summary = Assert.Single(sections);
        var entry = Assert.Single(summary.Entries);

        Assert.Contains("Full-Stack Software Developer", entry.Summary, StringComparison.Ordinal);
        Assert.Contains("Focused on healthcare", entry.Summary, StringComparison.Ordinal);
    }
}
