using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredUpdateServiceTests
{
    [Fact]
    public async Task UpdateWithAiAsync_RejectsBlankInstructions()
    {
        var service = new CvStructuredUpdateService(
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
        var service = new CvStructuredUpdateService(
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
        var service = new CvStructuredUpdateService(
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
        var service = new CvStructuredUpdateService(
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

    private sealed class CapturingUpdateAiClient : ICvStructuredUpdateAiClient
    {
        public List<string> Instructions { get; } = [];
        public IReadOnlyList<Guid>? FocusSectionIds { get; private set; }

        public Task<SaveCvStructuredDocumentRequest> UpdateAsync(
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

            return Task.FromResult(new SaveCvStructuredDocumentRequest(sections));
        }
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
        public Task<SaveCvStructuredDocumentRequest> UpdateAsync(
            CvStructuredDocumentDto current,
            string instructions,
            IReadOnlyList<Guid>? focusSectionIds = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("AI client should not be called.");
    }
}
