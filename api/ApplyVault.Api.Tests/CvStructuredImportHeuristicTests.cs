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
        Assert.Equal(["English", "Danish"], skills.Entries[0].Bullets);
        Assert.Equal("Frameworks", skills.Entries[1].Title);
        Assert.Equal([".NET", "Angular"], skills.Entries[1].Bullets);
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
}
