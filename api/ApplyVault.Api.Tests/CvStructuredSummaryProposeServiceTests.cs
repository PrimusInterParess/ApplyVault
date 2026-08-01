using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredSummaryProposeServiceTests
{
    [Fact]
    public async Task ProposeAsync_RejectsMissingStructuredContent()
    {
        var service = new CvStructuredSummaryProposeService(
            new EmptyStructuredDocumentService(),
            new ThrowingProposeAiClient());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ProposeAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new ProposeCvSummaryRequest()));
    }

    [Fact]
    public async Task ProposeAsync_RejectsEmptySections()
    {
        var service = new CvStructuredSummaryProposeService(
            new StubStructuredDocumentService(CreateEmptyDocument()),
            new ThrowingProposeAiClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProposeAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new ProposeCvSummaryRequest()));

        Assert.Contains("Import or create structured CV sections", exception.Message);
    }

    [Fact]
    public async Task ProposeAsync_RejectsEmptyProposedSummaryText()
    {
        var document = CreateDocumentWithSummary(Guid.NewGuid(), "Current summary");
        var service = new CvStructuredSummaryProposeService(
            new StubStructuredDocumentService(document),
            new FixedProposeAiClient(
                new CvSummaryProposeAiResult("   ", ["Rewrote opening"])));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProposeAsync(
                new AppUserEntity { SupabaseUserId = "user" },
                new ProposeCvSummaryRequest()));

        Assert.Contains("proposed summary", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProposeAsync_DoesNotCallSave()
    {
        var document = CreateDocumentWithSummary(Guid.NewGuid(), "Current summary");
        var structured = new CapturingStructuredDocumentService(document);
        var service = new CvStructuredSummaryProposeService(
            structured,
            new FixedProposeAiClient(
                new CvSummaryProposeAiResult(
                    "Proposed summary",
                    ["Emphasized backend leadership"])));

        await service.ProposeAsync(
            new AppUserEntity { SupabaseUserId = "user" },
            new ProposeCvSummaryRequest());

        Assert.False(structured.SaveWasCalled);
    }

    [Fact]
    public async Task ProposeAsync_PassesAppUserIdentityAndOptionalInstructionsToAiClient()
    {
        var summarySectionId = Guid.NewGuid();
        var document = CreateDocumentWithSummary(summarySectionId, "Current summary");
        var aiClient = new CapturingProposeAiClient(
            new CvSummaryProposeAiResult(
                "Proposed summary",
                ["Tightened length"]));
        var service = new CvStructuredSummaryProposeService(
            new StubStructuredDocumentService(document),
            aiClient);

        var result = await service.ProposeAsync(
            new AppUserEntity
            {
                SupabaseUserId = "user",
                DisplayName = "Ada Lovelace",
                Email = "ada@example.com"
            },
            new ProposeCvSummaryRequest("Emphasize leadership"));

        Assert.Equal("Ada Lovelace", aiClient.AppUserDisplayName);
        Assert.Equal("ada@example.com", aiClient.AppUserEmail);
        Assert.Equal("Emphasize leadership", aiClient.Instructions);
        Assert.Same(document, aiClient.Current);
        Assert.Equal(document.DocumentId, result.DocumentId);
        Assert.Equal(summarySectionId, result.SummarySectionId);
        Assert.Equal("Current summary", result.CurrentSummaryText);
        Assert.Equal("Proposed summary", result.ProposedSummaryText);
        Assert.Equal(["Tightened length"], result.ChangeBullets);
    }

    [Fact]
    public async Task ProposeAsync_AllowsNullOrBlankInstructions()
    {
        var document = CreateDocumentWithSummary(Guid.NewGuid(), "Current summary");
        var aiClient = new CapturingProposeAiClient(
            new CvSummaryProposeAiResult(
                "Proposed summary",
                ["Created sharper summary"]));
        var service = new CvStructuredSummaryProposeService(
            new StubStructuredDocumentService(document),
            aiClient);

        await service.ProposeAsync(
            new AppUserEntity { SupabaseUserId = "user" },
            new ProposeCvSummaryRequest("   "));

        Assert.Null(aiClient.Instructions);
    }

    [Fact]
    public async Task ProposeAsync_ReturnsNullSummarySectionIdWhenSummaryMissing()
    {
        var experienceId = Guid.NewGuid();
        var document = new CvStructuredDocumentDto(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [
                new CvStructuredSectionDto(
                    experienceId,
                    "Experience",
                    CvSectionTypes.Experience,
                    0,
                    [
                        CreateEntry("Built services.")
                    ])
            ]);
        var service = new CvStructuredSummaryProposeService(
            new StubStructuredDocumentService(document),
            new FixedProposeAiClient(
                new CvSummaryProposeAiResult(
                    "New summary from CV",
                    ["Created initial summary from CV"])));

        var result = await service.ProposeAsync(
            new AppUserEntity { SupabaseUserId = "user" },
            new ProposeCvSummaryRequest());

        Assert.Null(result.SummarySectionId);
        Assert.Equal(string.Empty, result.CurrentSummaryText);
        Assert.Equal("New summary from CV", result.ProposedSummaryText);
    }

    [Fact]
    public async Task ProposeAsync_UsesFirstSummarySectionBySortOrder()
    {
        var secondSummaryId = Guid.NewGuid();
        var firstSummaryId = Guid.NewGuid();
        var document = new CvStructuredDocumentDto(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [
                new CvStructuredSectionDto(
                    secondSummaryId,
                    "Summary",
                    CvSectionTypes.Summary,
                    2,
                    [CreateEntry("Second summary")]),
                new CvStructuredSectionDto(
                    firstSummaryId,
                    "Summary",
                    CvSectionTypes.Summary,
                    1,
                    [CreateEntry("First summary")])
            ]);
        var service = new CvStructuredSummaryProposeService(
            new StubStructuredDocumentService(document),
            new FixedProposeAiClient(
                new CvSummaryProposeAiResult(
                    "Proposed",
                    ["Updated tone"])));

        var result = await service.ProposeAsync(
            new AppUserEntity { SupabaseUserId = "user" },
            new ProposeCvSummaryRequest());

        Assert.Equal(firstSummaryId, result.SummarySectionId);
        Assert.Equal("First summary", result.CurrentSummaryText);
    }

    [Fact]
    public async Task ProposeAsync_NormalizesChangeBullets()
    {
        var longBullet = new string('x', 250);
        var document = CreateDocumentWithSummary(Guid.NewGuid(), "Current");
        var service = new CvStructuredSummaryProposeService(
            new StubStructuredDocumentService(document),
            new FixedProposeAiClient(
                new CvSummaryProposeAiResult(
                    "Proposed",
                    [
                        "  Keep me  ",
                        " ",
                        longBullet,
                        "b2",
                        "b3",
                        "b4",
                        "b5",
                        "b6-dropped"
                    ])));

        var result = await service.ProposeAsync(
            new AppUserEntity { SupabaseUserId = "user" },
            new ProposeCvSummaryRequest());

        Assert.Equal(5, result.ChangeBullets.Count);
        Assert.Equal("Keep me", result.ChangeBullets[0]);
        Assert.Equal(200, result.ChangeBullets[1].Length);
        Assert.DoesNotContain("b6-dropped", result.ChangeBullets);
    }

    private static CvStructuredDocumentDto CreateEmptyDocument() =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, []);

    private static CvStructuredDocumentDto CreateDocumentWithSummary(Guid summarySectionId, string summaryText) =>
        new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [
                new CvStructuredSectionDto(
                    summarySectionId,
                    "Summary",
                    CvSectionTypes.Summary,
                    0,
                    [CreateEntry(summaryText)])
            ]);

    private static CvStructuredEntryDto CreateEntry(string summary) =>
        new(
            Guid.NewGuid(),
            string.Empty,
            null,
            null,
            summary,
            [],
            string.Empty,
            new Dictionary<string, object?>(),
            CvEntrySources.Manual,
            null,
            0);

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
            throw new InvalidOperationException("Summary propose should not save structured content.");
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
            return Task.FromResult(structured);
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
            throw new InvalidOperationException("Summary propose should not save missing content.");
    }

    private sealed class FixedProposeAiClient(CvSummaryProposeAiResult result) : ICvStructuredSummaryProposeAiClient
    {
        public Task<CvSummaryProposeAiResult> ProposeAsync(
            CvStructuredDocumentDto current,
            string? instructions,
            string? appUserDisplayName,
            string? appUserEmail,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class CapturingProposeAiClient(CvSummaryProposeAiResult result) : ICvStructuredSummaryProposeAiClient
    {
        public CvStructuredDocumentDto? Current { get; private set; }
        public string? Instructions { get; private set; }
        public string? AppUserDisplayName { get; private set; }
        public string? AppUserEmail { get; private set; }

        public Task<CvSummaryProposeAiResult> ProposeAsync(
            CvStructuredDocumentDto current,
            string? instructions,
            string? appUserDisplayName,
            string? appUserEmail,
            CancellationToken cancellationToken = default)
        {
            Current = current;
            Instructions = instructions;
            AppUserDisplayName = appUserDisplayName;
            AppUserEmail = appUserEmail;
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingProposeAiClient : ICvStructuredSummaryProposeAiClient
    {
        public Task<CvSummaryProposeAiResult> ProposeAsync(
            CvStructuredDocumentDto current,
            string? instructions,
            string? appUserDisplayName,
            string? appUserEmail,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("AI client should not be called.");
    }
}
