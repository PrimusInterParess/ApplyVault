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
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("AI client should not be called.");
    }
}
