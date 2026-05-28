using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredSuggestionsServiceTests
{
    [Fact]
    public async Task GenerateAsync_RejectsInvalidSuggestionCount()
    {
        var service = new CvStructuredSuggestionsService(
            new ThrowingStructuredDocumentService(),
            new ThrowingSuggestionsAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new GenerateCvImprovementSuggestionsRequest(MaxSuggestions: 0)));

        Assert.Contains("between 1 and 10", exception.Message);
    }

    [Fact]
    public async Task GenerateAsync_RejectsMissingStructuredContent()
    {
        var service = new CvStructuredSuggestionsService(
            new EmptyStructuredDocumentService(),
            new ThrowingSuggestionsAiClient());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GenerateAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new GenerateCvImprovementSuggestionsRequest()));
    }

    [Fact]
    public async Task GenerateAsync_RejectsUnknownSectionIds()
    {
        var sectionId = Guid.NewGuid();
        var service = new CvStructuredSuggestionsService(
            new StubStructuredDocumentService(CreateDocument(sectionId)),
            new ThrowingSuggestionsAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new GenerateCvImprovementSuggestionsRequest([Guid.NewGuid()])));

        Assert.Contains("selected CV sections were not found", exception.Message);
    }

    [Fact]
    public async Task GenerateAsync_PassesValidatedFocusSectionIdsToAiClient()
    {
        var sectionId = Guid.NewGuid();
        var aiClient = new CapturingSuggestionsAiClient();
        var service = new CvStructuredSuggestionsService(
            new StubStructuredDocumentService(CreateDocument(sectionId)),
            aiClient);

        await service.GenerateAsync(
            new AppUserEntity { SupabaseUserId = "user" },
            new GenerateCvImprovementSuggestionsRequest([sectionId, sectionId], MaxSuggestions: 4));

        Assert.Equal([sectionId], aiClient.FocusSectionIds);
        Assert.Equal(4, aiClient.MaxSuggestions);
        Assert.False(aiClient.SaveWasCalled);
    }

    private static CvStructuredDocumentDto CreateDocument(Guid sectionId) =>
        new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [
                new CvStructuredSectionDto(
                    sectionId,
                    "Experience",
                    "Experience",
                    0,
                    [])
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
            throw new InvalidOperationException("Suggestion generation should not save structured content.");
    }

    private sealed class CapturingSuggestionsAiClient : ICvStructuredSuggestionsAiClient
    {
        public IReadOnlyList<Guid>? FocusSectionIds { get; private set; }
        public int MaxSuggestions { get; private set; }
        public bool SaveWasCalled { get; private set; }

        public Task<CvImprovementSuggestionsDto> GenerateAsync(
            CvStructuredDocumentDto current,
            IReadOnlyList<Guid>? focusSectionIds = null,
            int maxSuggestions = 6,
            CancellationToken cancellationToken = default)
        {
            FocusSectionIds = focusSectionIds;
            MaxSuggestions = maxSuggestions;

            return Task.FromResult(new CvImprovementSuggestionsDto(current.DocumentId, current.StructuredImportedAt, []));
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
            throw new InvalidOperationException("Structured document service should not save suggestions.");
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

    private sealed class ThrowingSuggestionsAiClient : ICvStructuredSuggestionsAiClient
    {
        public Task<CvImprovementSuggestionsDto> GenerateAsync(
            CvStructuredDocumentDto current,
            IReadOnlyList<Guid>? focusSectionIds = null,
            int maxSuggestions = 6,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("AI client should not be called.");
    }
}
