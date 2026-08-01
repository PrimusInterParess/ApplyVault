using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredUpdateProposeServiceTests
{
    [Fact]
    public async Task ProposeAsync_RejectsBlankInstructions()
    {
        var service = new CvStructuredUpdateProposeService(
            new ThrowingStructuredDocumentService(),
            new ThrowingUpdateAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProposeAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new UpdateCvStructuredWithAiRequest("   ")));

        Assert.Contains("Describe what to update", exception.Message);
    }

    [Fact]
    public async Task ProposeAsync_RejectsMissingStructuredContent()
    {
        var service = new CvStructuredUpdateProposeService(
            new EmptyStructuredDocumentService(),
            new ThrowingUpdateAiClient());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ProposeAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new UpdateCvStructuredWithAiRequest("Make it shorter.")));
    }

    [Fact]
    public async Task ProposeAsync_DoesNotCallSave()
    {
        var summaryId = Guid.NewGuid();
        var document = new CvStructuredDocumentDto(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [
                EntrySection(summaryId, "Summary", CvSectionTypes.Summary, 0, "Old summary")
            ]);
        var structured = new CapturingStructuredDocumentService(document);
        var service = new CvStructuredUpdateProposeService(
            structured,
            new FixedUpdateAiClient(
                new SaveCvStructuredDocumentRequest(
                [
                    WriteSection(summaryId, "Summary", CvSectionTypes.Summary, 0, "New summary")
                ]),
                ["Rewrote summary tone."]));

        var proposal = await service.ProposeAsync(
            new AppUserEntity { SupabaseUserId = "user" },
            new UpdateCvStructuredWithAiRequest("Rewrite summary.", [summaryId]));

        Assert.Null(structured.LastSaved);
        Assert.Equal(document.DocumentId, proposal.DocumentId);
        Assert.Equal([summaryId], proposal.FocusSectionIds);
        Assert.Equal(["Rewrote summary tone."], proposal.ChangeBullets);
        Assert.Single(proposal.ProposedSections);
        Assert.Equal("New summary", proposal.ProposedSections[0].Entries[0].Summary);
    }

    [Fact]
    public async Task ProposeAsync_DerivesFallbackChangeBulletsWhenAiOmitsThem()
    {
        var experienceId = Guid.NewGuid();
        var document = new CvStructuredDocumentDto(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [
                EntrySection(experienceId, "Experience", CvSectionTypes.Experience, 0, "Acme")
            ]);
        var service = new CvStructuredUpdateProposeService(
            new StubStructuredDocumentService(document),
            new FixedUpdateAiClient(
                new SaveCvStructuredDocumentRequest(
                [
                    WriteSection(experienceId, "Experience", CvSectionTypes.Experience, 0, "Acme Senior")
                ]),
                []));

        var proposal = await service.ProposeAsync(
            new AppUserEntity { SupabaseUserId = "user" },
            new UpdateCvStructuredWithAiRequest("Make senior.", [experienceId]));

        Assert.Equal(["Updated Experience."], proposal.ChangeBullets);
    }

    [Fact]
    public async Task ProposeAsync_RejectsUnknownSectionIds()
    {
        var sectionId = Guid.NewGuid();
        var service = new CvStructuredUpdateProposeService(
            new StubStructuredDocumentService(
                new CvStructuredDocumentDto(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    [
                        EntrySection(sectionId, "Experience", CvSectionTypes.Experience, 0, "Acme")
                    ])),
            new ThrowingUpdateAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProposeAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new UpdateCvStructuredWithAiRequest("Make shorter.", [Guid.NewGuid()])));

        Assert.Contains("selected CV sections were not found", exception.Message);
    }

    [Fact]
    public async Task ProposeAsync_RejectsEmptyAiSections()
    {
        var summaryId = Guid.NewGuid();
        var service = new CvStructuredUpdateProposeService(
            new StubStructuredDocumentService(
                new CvStructuredDocumentDto(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    [
                        EntrySection(summaryId, "Summary", CvSectionTypes.Summary, 0, "S")
                    ])),
            new FixedUpdateAiClient(new SaveCvStructuredDocumentRequest([]), ["noop"]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProposeAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new UpdateCvStructuredWithAiRequest("Rewrite summary.")));

        Assert.Contains("AI did not return any structured CV sections", exception.Message);
    }

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

    private sealed class FixedUpdateAiClient(
        SaveCvStructuredDocumentRequest response,
        IReadOnlyList<string> changeBullets) : ICvStructuredUpdateAiClient
    {
        public Task<CvStructuredUpdateAiResult> UpdateAsync(
            CvStructuredDocumentDto current,
            string instructions,
            IReadOnlyList<Guid>? focusSectionIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CvStructuredUpdateAiResult(response, changeBullets));
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
            throw new InvalidOperationException("Save should not be called.");
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
            throw new InvalidOperationException("Save should not be called.");
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
