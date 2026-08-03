using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class InterviewPrepServiceTests
{
    [Fact]
    public async Task CreateTurnAsync_RejectsInvalidMode()
    {
        var service = CreateService(
            new ThrowingStructuredDocumentService(),
            new ThrowingScrapeResultStore(),
            new ThrowingInterviewPrepAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTurnAsync(
                CreateUser(),
                new InterviewPrepTurnRequest("technical", "Let's start.")));

        Assert.Contains("mode must be one of", exception.Message);
    }

    [Fact]
    public async Task CreateTurnAsync_RejectsInvalidLanguageMix()
    {
        var service = CreateService(
            new ThrowingStructuredDocumentService(),
            new ThrowingScrapeResultStore(),
            new ThrowingInterviewPrepAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTurnAsync(
                CreateUser(),
                new InterviewPrepTurnRequest("behavioral", "Let's start.", LanguageMix: "en+da")));

        Assert.Contains("languageMix must be one of", exception.Message);
    }

    [Fact]
    public async Task CreateTurnAsync_RejectsEmptyUserMessage()
    {
        var service = CreateService(
            new ThrowingStructuredDocumentService(),
            new ThrowingScrapeResultStore(),
            new ThrowingInterviewPrepAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTurnAsync(
                CreateUser(),
                new InterviewPrepTurnRequest("behavioral", "   ")));

        Assert.Contains("userMessage is required", exception.Message);
    }

    [Fact]
    public async Task CreateTurnAsync_RejectsMissingStructuredContent()
    {
        var service = CreateService(
            new EmptyStructuredDocumentService(),
            new ThrowingScrapeResultStore(),
            new ThrowingInterviewPrepAiClient());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateTurnAsync(
                CreateUser(),
                new InterviewPrepTurnRequest("behavioral", "Let's start.")));
    }

    [Fact]
    public async Task CreateTurnAsync_RejectsEmptySections()
    {
        var service = CreateService(
            new StubStructuredDocumentService(CreateEmptyDocument()),
            new ThrowingScrapeResultStore(),
            new ThrowingInterviewPrepAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTurnAsync(
                CreateUser(),
                new InterviewPrepTurnRequest("behavioral", "Let's start.")));

        Assert.Contains("Import or create structured CV sections", exception.Message);
    }

    [Fact]
    public async Task CreateTurnAsync_RejectsUnknownScrapeResultWithNotFound()
    {
        var service = CreateService(
            new StubStructuredDocumentService(CreateDocument(Guid.NewGuid())),
            new MissScrapeResultStore(),
            new ThrowingInterviewPrepAiClient());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateTurnAsync(
                CreateUser(),
                new InterviewPrepTurnRequest(
                    "behavioral",
                    "Let's start.",
                    ScrapeResultId: Guid.NewGuid())));
    }

    [Fact]
    public async Task CreateTurnAsync_PropagatesDisabledAiAsInvalidOperation()
    {
        var service = CreateService(
            new StubStructuredDocumentService(CreateDocument(Guid.NewGuid())),
            new ThrowingScrapeResultStore(),
            new DisabledInterviewPrepAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTurnAsync(
                CreateUser(),
                new InterviewPrepTurnRequest("behavioral", "Let's start.")));

        Assert.Contains("Google AI is disabled", exception.Message);
    }

    [Fact]
    public async Task CreateTurnAsync_PassesCappedInputsAndDoesNotSave()
    {
        var sectionId = Guid.NewGuid();
        var scrapeId = Guid.NewGuid();
        var aiClient = new CapturingInterviewPrepAiClient();
        var structured = new CapturingStructuredDocumentService(CreateDocument(sectionId));
        var scrapeStore = new StubScrapeResultStore(CreateScrape(scrapeId));

        var service = CreateService(
            structured,
            scrapeStore,
            aiClient,
            new InterviewPrepAiOptions
            {
                MaxPriorTurns = 2,
                MaxUserMessageChars = 10,
                MaxPriorTurnChars = 5,
                DefaultLanguageMix = "en"
            });

        var response = await service.CreateTurnAsync(
            CreateUser(),
            new InterviewPrepTurnRequest(
                "behavioral",
                " 0123456789EXTRA ",
                LanguageMix: null,
                ScrapeResultId: scrapeId,
                PriorTurns:
                [
                    new InterviewPrepPriorTurnDto("user", "aaaaaa", "interview"),
                    new InterviewPrepPriorTurnDto("coach", "bbbbbb", "interview"),
                    new InterviewPrepPriorTurnDto("user", "cccccc", "interview")
                ]));

        Assert.Equal("interview", response.Phase);
        Assert.Equal("Pediatric nurse", response.Inference.Role);
        Assert.Equal([], response.FollowUps);
        Assert.False(structured.SaveWasCalled);
        Assert.False(scrapeStore.SaveWasCalled);
        Assert.NotNull(aiClient.LastRequest);
        Assert.Equal("0123456789", aiClient.LastRequest!.UserMessage);
        Assert.Equal("en", aiClient.LastRequest.LanguageMix);
        Assert.Equal(2, aiClient.LastRequest.PriorTurns!.Count);
        Assert.Equal("bbbbb", aiClient.LastRequest.PriorTurns[0].Text);
        Assert.Equal("ccccc", aiClient.LastRequest.PriorTurns[1].Text);
        Assert.Equal("City Hospital", aiClient.LastRequest.Job!.CompanyName);
    }

    private static InterviewPrepService CreateService(
        ICvStructuredDocumentService structuredDocumentService,
        IScrapeResultStore scrapeResultStore,
        IInterviewPrepAiClient aiClient,
        InterviewPrepAiOptions? options = null) =>
        new(
            CreateDbContext(),
            structuredDocumentService,
            scrapeResultStore,
            aiClient,
            Microsoft.Extensions.Options.Options.Create(options ?? new InterviewPrepAiOptions()));

    private static ApplyVaultDbContext CreateDbContext()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplyVaultDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplyVaultDbContext(dbOptions);
    }

    private static AppUserEntity CreateUser() =>
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SupabaseUserId = "user"
        };

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

    private static SavedScrapeResult CreateScrape(Guid id) =>
        new(
            id,
            DateTimeOffset.UtcNow,
            false,
            null,
            null,
            [],
            new ScrapeResultDto(
                "Pediatric nurse",
                "https://example.com/job",
                "text",
                4,
                DateTimeOffset.UtcNow.ToString("O"),
                new JobDetailsDto(
                    "example.com",
                    "job",
                    "Pediatric nurse",
                    "City Hospital",
                    "Copenhagen",
                    "Care for pediatric patients.",
                    "Ward nursing role",
                    null,
                    [])),
            new CaptureQualityDto(
                CaptureReviewStatuses.NotRequired,
                false,
                1,
                new CaptureQualityFieldDto(null, null, null, 1, false, null),
                new CaptureQualityFieldDto(null, null, null, 1, false, null),
                new CaptureQualityFieldDto(null, null, null, 1, false, null),
                new CaptureQualityFieldDto(null, null, null, 1, false, null)),
            null);

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
            throw new InvalidOperationException("Interview Prep should not save structured content.");
    }

    private sealed class CapturingStructuredDocumentService(CvStructuredDocumentDto structured) : ICvStructuredDocumentService
    {
        public bool SaveWasCalled { get; private set; }

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
            SaveWasCalled = true;
            throw new InvalidOperationException("Interview Prep should not save structured content.");
        }
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
            throw new InvalidOperationException("Interview Prep should not save missing content.");
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
            throw new InvalidOperationException("Interview Prep should not save structured content.");
    }

    private sealed class StubScrapeResultStore(SavedScrapeResult scrape) : IScrapeResultStore
    {
        public bool SaveWasCalled { get; private set; }

        public Task<IReadOnlyCollection<SavedScrapeResult>> GetAllAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<SavedScrapeResult>>([scrape]);

        public Task<SavedScrapeResult?> GetByIdAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SavedScrapeResult?>(id == scrape.Id ? scrape : null);

        public Task<SavedScrapeResult?> GetByUrlAsync(
            Guid userId,
            string url,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SavedScrapeResult?>(null);

        public Task<SavedScrapeResult> SaveAsync(
            AssessedScrapeResult result,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            SaveWasCalled = true;
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");
        }

        public Task<SavedScrapeResult?> UpdateCaptureReviewAsync(
            Guid id,
            Guid userId,
            UpdateScrapeResultCaptureReviewRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");

        public Task<SavedScrapeResult?> SetRejectedAsync(
            Guid id,
            Guid userId,
            bool isRejected,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");

        public Task<SavedScrapeResult?> UpdateDescriptionAsync(
            Guid id,
            Guid userId,
            string description,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");

        public Task<SavedScrapeResult?> UpsertInterviewEventAsync(
            Guid id,
            Guid userId,
            UpdateInterviewEventRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");

        public Task<SavedScrapeResult?> ClearInterviewEventAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");

        public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");
    }

    private sealed class MissScrapeResultStore : IScrapeResultStore
    {
        public Task<IReadOnlyCollection<SavedScrapeResult>> GetAllAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<SavedScrapeResult>>([]);

        public Task<SavedScrapeResult?> GetByIdAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SavedScrapeResult?>(null);

        public Task<SavedScrapeResult?> GetByUrlAsync(
            Guid userId,
            string url,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SavedScrapeResult?>(null);

        public Task<SavedScrapeResult> SaveAsync(
            AssessedScrapeResult result,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");

        public Task<SavedScrapeResult?> UpdateCaptureReviewAsync(
            Guid id,
            Guid userId,
            UpdateScrapeResultCaptureReviewRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");

        public Task<SavedScrapeResult?> SetRejectedAsync(
            Guid id,
            Guid userId,
            bool isRejected,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");

        public Task<SavedScrapeResult?> UpdateDescriptionAsync(
            Guid id,
            Guid userId,
            string description,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");

        public Task<SavedScrapeResult?> UpsertInterviewEventAsync(
            Guid id,
            Guid userId,
            UpdateInterviewEventRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");

        public Task<SavedScrapeResult?> ClearInterviewEventAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");

        public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Interview Prep should not mutate scrape results.");
    }

    private sealed class ThrowingScrapeResultStore : IScrapeResultStore
    {
        public Task<IReadOnlyCollection<SavedScrapeResult>> GetAllAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Scrape store should not be called.");

        public Task<SavedScrapeResult?> GetByIdAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Scrape store should not be called.");

        public Task<SavedScrapeResult?> GetByUrlAsync(
            Guid userId,
            string url,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Scrape store should not be called.");

        public Task<SavedScrapeResult> SaveAsync(
            AssessedScrapeResult result,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Scrape store should not be called.");

        public Task<SavedScrapeResult?> UpdateCaptureReviewAsync(
            Guid id,
            Guid userId,
            UpdateScrapeResultCaptureReviewRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Scrape store should not be called.");

        public Task<SavedScrapeResult?> SetRejectedAsync(
            Guid id,
            Guid userId,
            bool isRejected,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Scrape store should not be called.");

        public Task<SavedScrapeResult?> UpdateDescriptionAsync(
            Guid id,
            Guid userId,
            string description,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Scrape store should not be called.");

        public Task<SavedScrapeResult?> UpsertInterviewEventAsync(
            Guid id,
            Guid userId,
            UpdateInterviewEventRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Scrape store should not be called.");

        public Task<SavedScrapeResult?> ClearInterviewEventAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Scrape store should not be called.");

        public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Scrape store should not be called.");
    }

    private sealed class CapturingInterviewPrepAiClient : IInterviewPrepAiClient
    {
        public InterviewPrepAiTurnRequest? LastRequest { get; private set; }

        public Task<InterviewPrepAiTurnResult> GenerateTurnAsync(
            InterviewPrepAiTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new InterviewPrepAiTurnResult(
                "interview",
                new InterviewPrepAiInference("Pediatric nurse", "mid", "competency_behavioral", false),
                "Tell me about a difficult handoff.",
                null,
                [],
                []));
        }
    }

    private sealed class DisabledInterviewPrepAiClient : IInterviewPrepAiClient
    {
        public Task<InterviewPrepAiTurnResult> GenerateTurnAsync(
            InterviewPrepAiTurnRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Google AI is disabled. Enable GoogleAi:Enabled to run Interview Prep turns.");
    }

    private sealed class ThrowingInterviewPrepAiClient : IInterviewPrepAiClient
    {
        public Task<InterviewPrepAiTurnResult> GenerateTurnAsync(
            InterviewPrepAiTurnRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("AI client should not be called.");
    }
}
