using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.HtmlExport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplyVault.Api.Tests;

public sealed class CvDocumentExportServiceCompactParityTests
{
    [Fact]
    public async Task ExportHtmlAsync_unlimited_maxPages_builds_at_compact_level_0_without_pdf_search()
    {
        var htmlBuilder = new RecordingHtmlDocumentBuilder();
        var dispatcher = new ScriptedRenderDispatcher();
        var service = CreateService(htmlBuilder, dispatcher, pageCountsByLevel: new Dictionary<int, int>());

        var result = await service.ExportHtmlAsync(CreateUser(), new CvPdfExportOptions(TemplateId: 1, MaxPages: null));

        Assert.Equal(0, result.CompactLevel);
        Assert.Equal(1, htmlBuilder.CallCount);
        Assert.Equal(0, htmlBuilder.LastOptions?.CompactLevel ?? 0);
        Assert.Equal(0, dispatcher.CallCount);
        Assert.Equal("html-level-0", result.Html);
        Assert.Null(result.Notice);
    }

    [Fact]
    public async Task ExportHtmlAsync_with_maxPages_uses_same_compact_level_pdf_ramp_would_select()
    {
        // Level 0 → 3 pages, level 1 → 2 pages, level 2 → 1 page (fits maxPages=1).
        var pageCounts = new Dictionary<int, int>
        {
            [0] = 3,
            [1] = 2,
            [2] = 1,
        };
        var htmlBuilder = new RecordingHtmlDocumentBuilder();
        var dispatcher = new ScriptedRenderDispatcher();
        var service = CreateService(htmlBuilder, dispatcher, pageCounts);

        var htmlResult = await service.ExportHtmlAsync(CreateUser(), new CvPdfExportOptions(TemplateId: 2, MaxPages: 1));
        var pdfResult = await service.ExportPdfAsync(CreateUser(), new CvPdfExportOptions(TemplateId: 2, MaxPages: 1));

        Assert.Equal(2, htmlResult.CompactLevel);
        Assert.Equal(2, htmlBuilder.LastOptions?.CompactLevel);
        Assert.Equal("html-level-2", htmlResult.Html);
        Assert.Contains("compacted", htmlResult.Notice, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, pdfResult.PageCount);
        Assert.False(pdfResult.ExceedsMaxPages);
        Assert.Contains("compacted", pdfResult.Notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportHtmlAsync_and_ExportPdfAsync_share_best_effort_level_when_limit_unreachable()
    {
        // Never fits maxPages=1; best effort is lowest page count (level 3 → 2 pages).
        var pageCounts = new Dictionary<int, int>
        {
            [0] = 5,
            [1] = 4,
            [2] = 3,
            [3] = 2,
            [4] = 2,
        };
        var htmlBuilder = new RecordingHtmlDocumentBuilder();
        var dispatcher = new ScriptedRenderDispatcher();
        var service = CreateService(htmlBuilder, dispatcher, pageCounts);

        var htmlResult = await service.ExportHtmlAsync(CreateUser(), new CvPdfExportOptions(TemplateId: 1, MaxPages: 1));
        var pdfResult = await service.ExportPdfAsync(CreateUser(), new CvPdfExportOptions(TemplateId: 1, MaxPages: 1));

        Assert.Equal(3, htmlResult.CompactLevel);
        Assert.Equal(3, htmlBuilder.LastOptions?.CompactLevel);
        Assert.Equal(2, pdfResult.PageCount);
        Assert.True(pdfResult.ExceedsMaxPages);
        Assert.Contains("2 pages after compacting", htmlResult.Notice, StringComparison.Ordinal);
        Assert.Contains("2 pages after compacting", pdfResult.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportPdfAsync_unlimited_maxPages_renders_once_at_compact_level_0()
    {
        var pageCounts = new Dictionary<int, int> { [0] = 4 };
        var htmlBuilder = new RecordingHtmlDocumentBuilder();
        var dispatcher = new ScriptedRenderDispatcher();
        var service = CreateService(htmlBuilder, dispatcher, pageCounts);

        var result = await service.ExportPdfAsync(CreateUser(), new CvPdfExportOptions(TemplateId: 1, MaxPages: null));

        Assert.Equal(1, dispatcher.CallCount);
        Assert.Equal(0, dispatcher.LastCompactLevel);
        Assert.Equal(4, result.PageCount);
        Assert.Null(result.MaxPages);
        Assert.False(result.ExceedsMaxPages);
        Assert.Equal(0, htmlBuilder.CallCount);
    }

    private static CvDocumentExportService CreateService(
        RecordingHtmlDocumentBuilder htmlBuilder,
        ScriptedRenderDispatcher dispatcher,
        IReadOnlyDictionary<int, int> pageCountsByLevel)
    {
        return new CvDocumentExportService(
            new StubStructuredDocumentService(),
            new StubCvDocumentService(),
            dispatcher,
            htmlBuilder,
            new ScriptedPageCounter(pageCountsByLevel),
            NullLogger<CvDocumentExportService>.Instance);
    }

    private static AppUserEntity CreateUser() =>
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SupabaseUserId = "test-user",
        };

    private sealed class StubStructuredDocumentService : ICvStructuredDocumentService
    {
        public Task<CvStructuredDocumentDto?> GetStructuredAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default)
        {
            var entry = new CvStructuredEntryDto(
                Guid.NewGuid(),
                "Engineer",
                "Acme",
                "2020 — Present",
                "Built things",
                ["Did work"],
                "C#",
                new Dictionary<string, object?>(),
                "manual",
                null,
                0);

            var section = new CvStructuredSectionDto(
                Guid.NewGuid(),
                "Experience",
                "experience",
                0,
                [entry]);

            return Task.FromResult<CvStructuredDocumentDto?>(
                new CvStructuredDocumentDto(Guid.NewGuid(), DateTimeOffset.UtcNow, [section]));
        }

        public Task<CvStructuredDocumentDto> SaveStructuredAsync(
            AppUserEntity user,
            SaveCvStructuredDocumentRequest request,
            bool markImported,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubCvDocumentService : ICvDocumentService
    {
        public Task<CvDocumentDto?> GetCurrentAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CvDocumentDto?>(null);

        public Task<CvDocumentUploadResultDto> UploadAsync(
            AppUserEntity user,
            IFormFile file,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CvDocumentContent?> OpenContentAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CvDocumentContent?>(null);

        public Task<CvDocumentContent?> OpenOriginalContentAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CvDocumentContent?>(null);

        public Task<CvDocumentContent?> OpenProfilePhotoAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CvDocumentContent?>(null);

        public Task<CvDocumentDto> UploadProfilePhotoAsync(
            AppUserEntity user,
            IFormFile file,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CvDocumentDto?> DeleteProfilePhotoAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CvDocumentDto> StartBlankAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CvDocumentDto?> UpdateExportPreferencesAsync(
            AppUserEntity user,
            CvExportPreferencesDto preferences,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingHtmlDocumentBuilder : ICvExportHtmlDocumentBuilder
    {
        public int CallCount { get; private set; }
        public CvPdfRenderOptions? LastOptions { get; private set; }

        public Task<string> BuildAsync(
            CvExportRenderRequest request,
            int templateId,
            CvPdfRenderOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastOptions = options ?? CvPdfRenderOptions.Normal;
            var level = LastOptions.CompactLevel;
            return Task.FromResult($"html-level-{level}");
        }
    }

    private sealed class ScriptedRenderDispatcher : ICvExportRenderDispatcher
    {
        public int CallCount { get; private set; }
        public int LastCompactLevel { get; private set; }

        public Task<byte[]> RenderAsync(
            CvExportRenderRequest request,
            int templateId,
            CvPdfRenderOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastCompactLevel = options?.CompactLevel ?? 0;
            // Encode compact level in a tiny fake "PDF" payload for the page counter.
            return Task.FromResult(new byte[] { (byte)LastCompactLevel });
        }
    }

    private sealed class ScriptedPageCounter(IReadOnlyDictionary<int, int> pageCountsByLevel) : ICvPdfPageCounter
    {
        public int CountPages(byte[] pdfBytes)
        {
            var level = pdfBytes.Length > 0 ? pdfBytes[0] : 0;
            return pageCountsByLevel[level];
        }
    }
}
