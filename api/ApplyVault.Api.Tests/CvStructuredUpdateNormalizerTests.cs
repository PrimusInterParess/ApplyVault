using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredUpdateNormalizerTests
{
    [Fact]
    public void Normalize_DropsUnknownSourceSummaryIds()
    {
        var knownSourceSummaryId = Guid.NewGuid();
        var current = new CvStructuredDocumentDto(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [
                new CvStructuredSectionDto(
                    Guid.NewGuid(),
                    "Experience",
                    "Experience",
                    0,
                    [
                        new CvStructuredEntryDto(
                            Guid.NewGuid(),
                            "Software Engineer",
                            "Acme",
                            null,
                            "Built services.",
                            [],
                            ".NET",
                            CvEntrySources.GitHubSummary,
                            knownSourceSummaryId,
                            0)
                    ])
            ]);

        var response = new CvStructuredUpdateAiResponse(
            [
                new CvStructuredUpdateAiSection(
                    current.Sections[0].Id.ToString(),
                    "Experience",
                    "Experience",
                    0,
                    [
                        new CvStructuredUpdateAiEntry(
                            current.Sections[0].Entries[0].Id.ToString(),
                            "Software Engineer",
                            "Acme",
                            null,
                            "Built reliable services.",
                            [],
                            ".NET",
                            CvEntrySources.GitHubSummary,
                            Guid.NewGuid().ToString(),
                            0),
                        new CvStructuredUpdateAiEntry(
                            null,
                            "Side Project",
                            null,
                            null,
                            "Built a side project.",
                            [],
                            "Angular",
                            CvEntrySources.Manual,
                            knownSourceSummaryId.ToString(),
                            1)
                    ])
            ]);

        var normalized = CvStructuredUpdateNormalizer.Normalize(current, response);

        Assert.Null(normalized.Sections[0].Entries[0].SourceSummaryId);
        Assert.Equal(knownSourceSummaryId, normalized.Sections[0].Entries[1].SourceSummaryId);
    }

    [Fact]
    public void Normalize_PreservesIdsAndReindexesSortOrder()
    {
        var sectionId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var request = CvStructuredUpdateNormalizer.Normalize(EmptyCurrentDocument, new CvStructuredUpdateAiResponse(
        [
            new CvStructuredUpdateAiSection(
                sectionId.ToString(),
                "Experience",
                CvSectionTypes.Experience,
                5,
                [
                    new CvStructuredUpdateAiEntry(
                        entryId.ToString(),
                        "Senior Software Engineer",
                        "Acme Corp",
                        "2020 - Present",
                        "Built reliable systems.",
                        ["- Reduced latency"],
                        "C#, PostgreSQL",
                        CvEntrySources.Import,
                        null,
                        9)
                ])
        ]));

        var section = Assert.Single(request.Sections);
        var entry = Assert.Single(section.Entries);

        Assert.Equal(sectionId, section.Id);
        Assert.Equal(0, section.SortOrder);
        Assert.Equal(entryId, entry.Id);
        Assert.Equal(0, entry.SortOrder);
        Assert.Equal(["Reduced latency"], entry.Bullets);
    }

    [Fact]
    public void Normalize_DropsEmptyEntriesAndUsesManualSourceWhenMissing()
    {
        var request = CvStructuredUpdateNormalizer.Normalize(EmptyCurrentDocument, new CvStructuredUpdateAiResponse(
        [
            new CvStructuredUpdateAiSection(
                "not-a-guid",
                "Projects",
                "Unknown",
                0,
                [
                    new CvStructuredUpdateAiEntry(
                        null,
                        string.Empty,
                        null,
                        null,
                        string.Empty,
                        [],
                        string.Empty,
                        null,
                        null,
                        0),
                    new CvStructuredUpdateAiEntry(
                        null,
                        "ApplyVault",
                        null,
                        null,
                        "Built a job application tracker.",
                        [],
                        "Angular, .NET",
                        null,
                        null,
                        1)
                ])
        ]));

        var section = Assert.Single(request.Sections);
        var entry = Assert.Single(section.Entries);

        Assert.Null(section.Id);
        Assert.Equal(CvSectionTypes.Custom, section.SectionType);
        Assert.Equal(CvEntrySources.Manual, entry.Source);
    }

    private static CvStructuredDocumentDto EmptyCurrentDocument { get; } =
        new(Guid.NewGuid(), null, []);
}
