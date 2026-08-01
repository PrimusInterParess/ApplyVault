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
                            new Dictionary<string, object?>(),
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

    [Fact]
    public void MergeAssistUpdate_WithFocus_ReplacesOnlyFocusedAndPreservesOthers()
    {
        var contactId = Guid.NewGuid();
        var summaryId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();
        var educationId = Guid.NewGuid();

        var current = Document(
            Section(contactId, "Contact", CvSectionTypes.Contact, 0, "Alice"),
            Section(summaryId, "Summary", CvSectionTypes.Summary, 1, "Old summary"),
            Section(experienceId, "Experience", CvSectionTypes.Experience, 2, "Acme"),
            Section(educationId, "Education", CvSectionTypes.Education, 3, "Uni"));

        // Partial AI payload: emptied Contact (would wipe if persisted raw) + Summary update.
        var aiResult = new SaveCvStructuredDocumentRequest(
        [
            WriteSection(contactId, "Contact", CvSectionTypes.Contact, 0, string.Empty),
            WriteSection(summaryId, "Summary", CvSectionTypes.Summary, 1, "New AI summary")
        ]);

        var merged = CvStructuredUpdateNormalizer.MergeAssistUpdate(
            current,
            aiResult,
            [summaryId]);

        Assert.Equal(
            [contactId, summaryId, experienceId, educationId],
            merged.Sections.Select((section) => section.Id!.Value).ToArray());
        Assert.Equal("Alice", EntryMarker(merged, contactId));
        Assert.Equal("New AI summary", EntryMarker(merged, summaryId));
        Assert.Equal("Acme", EntryMarker(merged, experienceId));
        Assert.Equal("Uni", EntryMarker(merged, educationId));
        Assert.Equal([0, 1, 2, 3], merged.Sections.Select((section) => section.SortOrder).ToArray());
    }

    [Fact]
    public void MergeAssistUpdate_WithFocus_IgnoresNonFocusedAiSections()
    {
        var contactId = Guid.NewGuid();
        var summaryId = Guid.NewGuid();

        var current = Document(
            Section(contactId, "Contact", CvSectionTypes.Contact, 0, "Bob"),
            Section(summaryId, "Summary", CvSectionTypes.Summary, 1, "Old"));

        var aiResult = new SaveCvStructuredDocumentRequest(
        [
            WriteSection(contactId, "Contact", CvSectionTypes.Contact, 0, string.Empty),
            WriteSection(summaryId, "Summary", CvSectionTypes.Summary, 1, "Updated")
        ]);

        var merged = CvStructuredUpdateNormalizer.MergeAssistUpdate(
            current,
            aiResult,
            [summaryId]);

        Assert.Equal("Bob", EntryMarker(merged, contactId));
        Assert.Equal("Updated", EntryMarker(merged, summaryId));
    }

    [Fact]
    public void MergeAssistUpdate_WithoutFocus_PreservesOmittedCurrentSections()
    {
        var summaryId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();
        var educationId = Guid.NewGuid();

        var current = Document(
            Section(summaryId, "Summary", CvSectionTypes.Summary, 0, "S"),
            Section(experienceId, "Experience", CvSectionTypes.Experience, 1, "Acme"),
            Section(educationId, "Education", CvSectionTypes.Education, 2, "Uni"));

        var aiResult = new SaveCvStructuredDocumentRequest(
        [
            WriteSection(summaryId, "Summary", CvSectionTypes.Summary, 0, "Rewritten")
        ]);

        var merged = CvStructuredUpdateNormalizer.MergeAssistUpdate(current, aiResult, focusSectionIds: null);

        Assert.Equal(
            [summaryId, experienceId, educationId],
            merged.Sections.Select((section) => section.Id!.Value).ToArray());
        Assert.Equal("Rewritten", EntryMarker(merged, summaryId));
        Assert.Equal("Acme", EntryMarker(merged, experienceId));
        Assert.Equal("Uni", EntryMarker(merged, educationId));
    }

    [Fact]
    public void MergeAssistUpdate_WithoutFocus_AppendsAiOnlyNewSections()
    {
        var summaryId = Guid.NewGuid();
        var skillsId = Guid.NewGuid();

        var current = Document(
            Section(summaryId, "Summary", CvSectionTypes.Summary, 0, "S"));

        var aiResult = new SaveCvStructuredDocumentRequest(
        [
            WriteSection(summaryId, "Summary", CvSectionTypes.Summary, 0, "S2"),
            WriteSection(skillsId, "Skills", CvSectionTypes.Skills, 1, "TS")
        ]);

        var merged = CvStructuredUpdateNormalizer.MergeAssistUpdate(current, aiResult, focusSectionIds: null);

        Assert.Equal(
            [summaryId, skillsId],
            merged.Sections.Select((section) => section.Id!.Value).ToArray());
        Assert.Equal("S2", EntryMarker(merged, summaryId));
        Assert.Equal("TS", EntryMarker(merged, skillsId));
        Assert.Equal([0, 1], merged.Sections.Select((section) => section.SortOrder).ToArray());
    }

    [Fact]
    public void MergeAssistUpdate_ReturnsAiResultWhenCurrentHasNoSections()
    {
        var summaryId = Guid.NewGuid();
        var aiResult = new SaveCvStructuredDocumentRequest(
        [
            WriteSection(summaryId, "Summary", CvSectionTypes.Summary, 5, "Only")
        ]);

        var merged = CvStructuredUpdateNormalizer.MergeAssistUpdate(
            EmptyCurrentDocument,
            aiResult,
            [summaryId]);

        var section = Assert.Single(merged.Sections);
        Assert.Equal(summaryId, section.Id);
        Assert.Equal(0, section.SortOrder);
        Assert.Equal("Only", EntryMarker(merged, summaryId));
    }

    private static CvStructuredDocumentDto EmptyCurrentDocument { get; } =
        new(Guid.NewGuid(), null, []);

    private static CvStructuredDocumentDto Document(params CvStructuredSectionDto[] sections) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, sections);

    private static CvStructuredSectionDto Section(
        Guid id,
        string heading,
        string sectionType,
        int sortOrder,
        string marker) =>
        new(
            id,
            heading,
            sectionType,
            sortOrder,
            [
                new CvStructuredEntryDto(
                    Guid.NewGuid(),
                    marker,
                    null,
                    null,
                    marker,
                    [],
                    string.Empty,
                    new Dictionary<string, object?>(),
                    CvEntrySources.Manual,
                    null,
                    0)
            ]);

    private static CvStructuredSectionWriteDto WriteSection(
        Guid id,
        string heading,
        string sectionType,
        int sortOrder,
        string marker) =>
        new(
            id,
            heading,
            sectionType,
            sortOrder,
            [
                new CvStructuredEntryWriteDto(
                    Guid.NewGuid(),
                    marker,
                    null,
                    null,
                    marker,
                    [],
                    string.Empty,
                    CvEntrySources.Manual,
                    null,
                    0)
            ]);

    private static string EntryMarker(SaveCvStructuredDocumentRequest request, Guid sectionId) =>
        request.Sections.Single((section) => section.Id == sectionId).Entries[0].Summary;
}
