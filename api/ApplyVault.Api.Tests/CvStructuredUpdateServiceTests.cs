using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredUpdateServiceTests
{
    [Fact]
    public async Task UpdateWithAiAsync_RejectsBlankInstructions()
    {
        var service = CreateService(
            new ThrowingStructuredDocumentService(),
            new ThrowingUpdateAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateWithAiAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new UpdateCvStructuredWithAiRequest("   ")));

        Assert.Contains("Describe what to update", exception.Message);
    }

    [Fact]
    public async Task UpdateWithAiAsync_RejectsMissingStructuredContent()
    {
        var service = CreateService(
            new EmptyStructuredDocumentService(),
            new ThrowingUpdateAiClient());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateWithAiAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new UpdateCvStructuredWithAiRequest("Make it shorter.")));
    }

    [Fact]
    public async Task UpdateWithAiAsync_RejectsUnknownSectionIds()
    {
        var sectionId = Guid.NewGuid();
        var service = CreateService(
            new StubStructuredDocumentService(
                new CvStructuredDocumentDto(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    [
                        new CvStructuredSectionDto(
                            sectionId,
                            "Experience",
                            "Experience",
                            0,
                            [])
                    ])),
            new ThrowingUpdateAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateWithAiAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new UpdateCvStructuredWithAiRequest(
                    "Make it shorter.",
                    [Guid.NewGuid()])));

        Assert.Contains("selected CV sections were not found", exception.Message);
    }

    [Fact]
    public async Task UpdateWithAiAsync_PassesValidatedFocusSectionIdsToAiClient()
    {
        var sectionId = Guid.NewGuid();
        var aiClient = new CapturingUpdateAiClient();
        var service = CreateService(
            new StubStructuredDocumentService(
                new CvStructuredDocumentDto(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    [
                        new CvStructuredSectionDto(
                            sectionId,
                            "Experience",
                            "Experience",
                            0,
                            [])
                    ])),
            aiClient);

        await service.UpdateWithAiAsync(
            new AppUserEntity { SupabaseUserId = "user" },
            new UpdateCvStructuredWithAiRequest(
                "Make it shorter.",
                [sectionId, sectionId]));

        Assert.Equal(["Make it shorter."], aiClient.Instructions);
        Assert.Equal([sectionId], aiClient.FocusSectionIds);
    }

    [Fact]
    public async Task UpdateWithAiAsync_MergesFocusPartialBeforePersist()
    {
        var contactId = Guid.NewGuid();
        var summaryId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();
        var document = new CvStructuredDocumentDto(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [
                EntrySection(contactId, "Contact", CvSectionTypes.Contact, 0, "Alice"),
                EntrySection(summaryId, "Summary", CvSectionTypes.Summary, 1, "Old summary"),
                EntrySection(experienceId, "Experience", CvSectionTypes.Experience, 2, "Acme")
            ]);

        var structured = new CapturingStructuredDocumentService(document);
        var aiClient = new FixedUpdateAiClient(new SaveCvStructuredDocumentRequest(
        [
            WriteSection(contactId, "Contact", CvSectionTypes.Contact, 0, string.Empty),
            WriteSection(summaryId, "Summary", CvSectionTypes.Summary, 1, "New AI summary")
        ]));

        var service = CreateService(structured, aiClient);

        await service.UpdateWithAiAsync(
            new AppUserEntity { SupabaseUserId = "user" },
            new UpdateCvStructuredWithAiRequest("Rewrite summary.", [summaryId]));

        Assert.NotNull(structured.LastSaved);
        Assert.Equal(7, structured.LastSaved!.Sections.Count);
        Assert.Equal("Alice", Marker(structured.LastSaved, contactId));
        Assert.Equal("New AI summary", Marker(structured.LastSaved, summaryId));
        Assert.Equal("Acme", Marker(structured.LastSaved, experienceId));
    }

    [Fact]
    public async Task UpdateWithAiAsync_MergesOmitPreserveWithoutFocusBeforePersist()
    {
        var summaryId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();
        var document = new CvStructuredDocumentDto(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [
                EntrySection(summaryId, "Summary", CvSectionTypes.Summary, 0, "S"),
                EntrySection(experienceId, "Experience", CvSectionTypes.Experience, 1, "Acme")
            ]);

        var structured = new CapturingStructuredDocumentService(document);
        var aiClient = new FixedUpdateAiClient(new SaveCvStructuredDocumentRequest(
        [
            WriteSection(summaryId, "Summary", CvSectionTypes.Summary, 0, "Rewritten")
        ]));

        var service = CreateService(structured, aiClient);

        await service.UpdateWithAiAsync(
            new AppUserEntity { SupabaseUserId = "user" },
            new UpdateCvStructuredWithAiRequest("Rewrite summary."));

        Assert.NotNull(structured.LastSaved);
        Assert.Equal(7, structured.LastSaved!.Sections.Count);
        Assert.Equal("Rewritten", Marker(structured.LastSaved, summaryId));
        Assert.Equal("Acme", Marker(structured.LastSaved, experienceId));
    }

    [Fact]
    public async Task UpdateWithAiAsync_RejectsEmptyAiSectionsBeforePersist()
    {
        var summaryId = Guid.NewGuid();
        var service = CreateService(
            new StubStructuredDocumentService(
                new CvStructuredDocumentDto(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    [
                        EntrySection(summaryId, "Summary", CvSectionTypes.Summary, 0, "S")
                    ])),
            new FixedUpdateAiClient(new SaveCvStructuredDocumentRequest([])));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateWithAiAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new UpdateCvStructuredWithAiRequest("Rewrite summary.")));

        Assert.Contains("AI did not return any structured CV sections", exception.Message);
    }

    private static readonly ICvSectionCatalog Catalog = CvSectionCatalogProvider.LoadFromDefaultPath();

    private static CvStructuredUpdateService CreateService(
        ICvStructuredDocumentService structured,
        ICvStructuredUpdateAiClient aiClient) =>
        new(structured, aiClient, Catalog);

    private static CvStructuredSectionDto EntrySection(
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

    private static string Marker(SaveCvStructuredDocumentRequest request, Guid sectionId) =>
        request.Sections.Single((section) => section.Id == sectionId).Entries[0].Summary;

    private sealed class StubStructuredDocumentService(CvStructuredDocumentDto structured) : ICvStructuredDocumentService
    {
        public Task<CvStructuredDocumentDto?> GetStructuredAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CvStructuredDocumentDto?>(structured);

        public Task<CvStructuredDocumentDto> SaveStructuredAsync(
            AppUserEntity user,
            SaveCvStructuredDocumentRequest request,
            bool markImported,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(structured);
    }

    private sealed class CapturingStructuredDocumentService(CvStructuredDocumentDto structured) : ICvStructuredDocumentService
    {
        public SaveCvStructuredDocumentRequest? LastSaved { get; private set; }

        public Task<CvStructuredDocumentDto?> GetStructuredAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CvStructuredDocumentDto?>(structured);

        public Task<CvStructuredDocumentDto> SaveStructuredAsync(
            AppUserEntity user,
            SaveCvStructuredDocumentRequest request,
            bool markImported,
            CancellationToken cancellationToken = default)
        {
            LastSaved = request;
            return Task.FromResult(structured);
        }
    }

    private sealed class CapturingUpdateAiClient : ICvStructuredUpdateAiClient
    {
        public List<string> Instructions { get; } = [];
        public IReadOnlyList<Guid>? FocusSectionIds { get; private set; }

        public Task<CvStructuredUpdateAiResult> UpdateAsync(
            CvStructuredDocumentDto current,
            string instructions,
            IReadOnlyList<Guid>? focusSectionIds = null,
            CancellationToken cancellationToken = default)
        {
            Instructions.Add(instructions);
            FocusSectionIds = focusSectionIds;

            var sections = current.Sections
                .Select((section, sectionIndex) => new CvStructuredSectionWriteDto(
                    section.Id,
                    section.Heading,
                    section.SectionType,
                    sectionIndex,
                    []))
                .ToArray();

            return Task.FromResult(new CvStructuredUpdateAiResult(
                new SaveCvStructuredDocumentRequest(sections),
                ["Updated experience."]));
        }
    }

    private sealed class FixedUpdateAiClient(SaveCvStructuredDocumentRequest response) : ICvStructuredUpdateAiClient
    {
        public Task<CvStructuredUpdateAiResult> UpdateAsync(
            CvStructuredDocumentDto current,
            string instructions,
            IReadOnlyList<Guid>? focusSectionIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CvStructuredUpdateAiResult(response, ["Updated summary."]));
    }

    private sealed class ThrowingStructuredDocumentService : ICvStructuredDocumentService
    {
        public Task<CvStructuredDocumentDto?> GetStructuredAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Structured document service should not be called.");

        public Task<CvStructuredDocumentDto> SaveStructuredAsync(
            AppUserEntity user,
            SaveCvStructuredDocumentRequest request,
            bool markImported,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Structured document service should not be called.");
    }

    private sealed class EmptyStructuredDocumentService : ICvStructuredDocumentService
    {
        public Task<CvStructuredDocumentDto?> GetStructuredAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CvStructuredDocumentDto?>(null);

        public Task<CvStructuredDocumentDto> SaveStructuredAsync(
            AppUserEntity user,
            SaveCvStructuredDocumentRequest request,
            bool markImported,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Structured document service should not save missing content.");
    }

    private sealed class ThrowingUpdateAiClient : ICvStructuredUpdateAiClient
    {
        public Task<CvStructuredUpdateAiResult> UpdateAsync(
            CvStructuredDocumentDto current,
            string instructions,
            IReadOnlyList<Guid>? focusSectionIds = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("AI client should not be called.");
    }
}
