using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredEvaluationServiceTests
{
    [Fact]
    public async Task EvaluateAsync_RejectsInvalidMaxFindings()
    {
        var service = new CvStructuredEvaluationService(
            new ThrowingStructuredDocumentService(),
            new ThrowingEvaluationAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EvaluateAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new EvaluateCvQualityRequest(MaxFindings: 0)));

        Assert.Contains("between 1 and 20", exception.Message);
    }

    [Fact]
    public async Task EvaluateAsync_RejectsMaxFindingsAboveBound()
    {
        var service = new CvStructuredEvaluationService(
            new ThrowingStructuredDocumentService(),
            new ThrowingEvaluationAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EvaluateAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new EvaluateCvQualityRequest(MaxFindings: 21)));

        Assert.Contains("between 1 and 20", exception.Message);
    }

    [Fact]
    public async Task EvaluateAsync_RejectsMissingStructuredContent()
    {
        var service = new CvStructuredEvaluationService(
            new EmptyStructuredDocumentService(),
            new ThrowingEvaluationAiClient());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.EvaluateAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new EvaluateCvQualityRequest()));
    }

    [Fact]
    public async Task EvaluateAsync_RejectsEmptySections()
    {
        var service = new CvStructuredEvaluationService(
            new StubStructuredDocumentService(CreateEmptyDocument()),
            new ThrowingEvaluationAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EvaluateAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new EvaluateCvQualityRequest()));

        Assert.Contains("Import or create structured CV sections", exception.Message);
    }

    [Fact]
    public async Task EvaluateAsync_PassesValidatedMaxFindingsToAiClient()
    {
        var sectionId = Guid.NewGuid();
        var aiClient = new CapturingEvaluationAiClient();
        var service = new CvStructuredEvaluationService(
            new StubStructuredDocumentService(CreateDocument(sectionId)),
            aiClient);

        await service.EvaluateAsync(
            new AppUserEntity { SupabaseUserId = "user" },
            new EvaluateCvQualityRequest(MaxFindings: 5));

        Assert.Equal(5, aiClient.MaxFindings);
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

    private static CvStructuredDocumentDto CreateEmptyDocument() =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, []);

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
            throw new InvalidOperationException("Evaluation should not save structured content.");
    }

    private sealed class CapturingEvaluationAiClient : ICvStructuredEvaluationAiClient
    {
        public int MaxFindings { get; private set; }
        public bool SaveWasCalled { get; private set; }

        public Task<CvQualityEvaluationDto> EvaluateAsync(
            CvStructuredDocumentDto current,
            int maxFindings = 8,
            CancellationToken cancellationToken = default)
        {
            MaxFindings = maxFindings;

            return Task.FromResult(new CvQualityEvaluationDto(
                current.DocumentId,
                70,
                "Summary",
                [
                    new CvQualityEvaluationDimensionDto("content", 70, "Content ok"),
                    new CvQualityEvaluationDimensionDto("structure", 70, "Structure ok"),
                    new CvQualityEvaluationDimensionDto("format", 70, "Format ok")
                ],
                [],
                []));
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
            throw new InvalidOperationException("Evaluation should not save structured content.");
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
            throw new InvalidOperationException("Evaluation should not save missing content.");
    }

    private sealed class ThrowingEvaluationAiClient : ICvStructuredEvaluationAiClient
    {
        public Task<CvQualityEvaluationDto> EvaluateAsync(
            CvStructuredDocumentDto current,
            int maxFindings = 8,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("AI client should not be called.");
    }
}
