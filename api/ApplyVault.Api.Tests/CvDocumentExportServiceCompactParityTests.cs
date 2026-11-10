using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.HtmlExport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplyVault.Api.Tests;

/// <summary>
/// Page policy: one page recommended, two pages maximum. HTML preview and PDF download must
/// resolve the same compact level for the same page-count profile.
/// </summary>
public sealed class CvDocumentExportServiceCompactParityTests
{
    [Fact]
    public async Task ExportHtmlAsync_and_ExportPdfAsync_use_same_compact_level_when_one_page_reachable()
    {
        // Level 0 → 3 pages, level 1 → 2 pages, level 2 → 1 page (hits the recommended target).
        var pageCounts = new Dictionary<int, int>
        {
            [0] = 3,
            [1] = 2,
            [2] = 1,
        };
        var htmlBuilder = new RecordingHtmlDocumentBuilder();
        var dispatcher = new ScriptedRenderDispatcher();
        var service = CreateService(htmlBuilder, dispatcher, pageCounts);

        var htmlResult = await service.ExportHtmlAsync(CreateUser());
        var pdfResult = await service.ExportPdfAsync(CreateUser());

        Assert.Equal(2, htmlResult.CompactLevel);
        Assert.Equal(2, htmlBuilder.LastOptions?.CompactLevel);
        Assert.Equal("html-level-2", htmlResult.Html);
        Assert.Equal("Layout was compacted to fit on one page.", htmlResult.Notice);
        Assert.Equal(1, pdfResult.PageCount);
        Assert.False(pdfResult.ExceedsMaxPages);
        Assert.Equal("Layout was compacted to fit on one page.", pdfResult.Notice);
    }

    [Fact]
    public async Task ExportPdfAsync_returns_level_0_when_already_one_page()
    {
        var pageCounts = new Dictionary<int, int> { [0] = 1 };
        var htmlBuilder = new RecordingHtmlDocumentBuilder();
        var dispatcher = new ScriptedRenderDispatcher();
        var service = CreateService(htmlBuilder, dispatcher, pageCounts);

        var result = await service.ExportPdfAsync(CreateUser());

        Assert.Equal(1, dispatcher.CallCount);
        Assert.Equal(0, dispatcher.LastCompactLevel);
        Assert.Equal(1, result.PageCount);
        Assert.False(result.ExceedsMaxPages);
        Assert.Null(result.Notice);
        Assert.Equal(0, htmlBuilder.CallCount);
    }

    [Fact]
    public async Task Export_steps_one_tighter_when_level_0_and_1_both_fit_two_pages()
    {
        // Two pages at every level. The search still walks the remaining levels looking for one
        // page, then keeps exactly one step past the loosest two-page fit (level 1).
        var pageCounts = new Dictionary<int, int>
        {
            [0] = 2,
            [1] = 2,
            [2] = 2,
            [3] = 2,
            [4] = 2,
        };
        var htmlBuilder = new RecordingHtmlDocumentBuilder();
        var dispatcher = new ScriptedRenderDispatcher();
        var service = CreateService(htmlBuilder, dispatcher, pageCounts);

        var pdfResult = await service.ExportPdfAsync(CreateUser());

        Assert.Equal(5, dispatcher.CallCount);
        Assert.Equal(2, pdfResult.PageCount);
        Assert.False(pdfResult.ExceedsMaxPages);
        Assert.Contains("one page is recommended", pdfResult.Notice, StringComparison.Ordinal);

        var htmlResult = await service.ExportHtmlAsync(CreateUser());

        Assert.Equal(1, htmlResult.CompactLevel);
        Assert.Equal("html-level-1", htmlResult.Html);
        Assert.Equal(1, htmlBuilder.LastOptions?.CompactLevel);
        Assert.Contains("one page is recommended", htmlResult.Notice, StringComparison.Ordinal);
        Assert.Equal(10, dispatcher.CallCount);
    }

    [Fact]
    public async Task Export_keeps_compacting_until_a_later_level_fits_on_one_page()
    {
        // Levels 0–3 stay on two pages. Level 4 reaches one page and is used.
        var pageCounts = new Dictionary<int, int>
        {
            [0] = 2,
            [1] = 2,
            [2] = 2,
            [3] = 2,
            [4] = 1,
        };
        var htmlBuilder = new RecordingHtmlDocumentBuilder();
        var dispatcher = new ScriptedRenderDispatcher();
        var service = CreateService(htmlBuilder, dispatcher, pageCounts);

        var pdfResult = await service.ExportPdfAsync(CreateUser());
        var htmlResult = await service.ExportHtmlAsync(CreateUser());

        Assert.Equal(10, dispatcher.CallCount);
        Assert.Equal(1, pdfResult.PageCount);
        Assert.False(pdfResult.ExceedsMaxPages);
        Assert.Equal("Layout was compacted to fit on one page.", pdfResult.Notice);
        Assert.Equal(4, htmlResult.CompactLevel);
        Assert.Equal("html-level-4", htmlResult.Html);
        Assert.Equal(pdfResult.Notice, htmlResult.Notice);
    }

    [Fact]
    public async Task Export_keeps_loosest_two_page_level_when_the_next_level_exceeds_two_pages()
    {
        var pageCounts = new Dictionary<int, int>
        {
            [0] = 3,
            [1] = 2,
            [2] = 3,
            [3] = 3,
            [4] = 3,
        };
        var htmlBuilder = new RecordingHtmlDocumentBuilder();
        var dispatcher = new ScriptedRenderDispatcher();
        var service = CreateService(htmlBuilder, dispatcher, pageCounts);

        var htmlResult = await service.ExportHtmlAsync(CreateUser());

        Assert.Equal(5, dispatcher.CallCount);
        Assert.Equal(1, htmlResult.CompactLevel);
        Assert.Equal(1, htmlBuilder.LastOptions?.CompactLevel);
        Assert.Equal(
            "Your CV is 2 pages; one page is recommended — consider trimming content.",
            htmlResult.Notice);
    }

    [Fact]
    public async Task Export_returns_level_4_when_it_is_the_only_two_page_fit()
    {
        var pageCounts = new Dictionary<int, int>
        {
            [0] = 3,
            [1] = 3,
            [2] = 4,
            [3] = 3,
            [4] = 2,
        };
        var htmlBuilder = new RecordingHtmlDocumentBuilder();
        var dispatcher = new ScriptedRenderDispatcher();
        var service = CreateService(htmlBuilder, dispatcher, pageCounts);

        var htmlResult = await service.ExportHtmlAsync(CreateUser());

        Assert.Equal(5, dispatcher.CallCount);
        Assert.Equal(4, htmlResult.CompactLevel);
        Assert.Equal(4, htmlBuilder.LastOptions?.CompactLevel);
        Assert.Contains("one page is recommended", htmlResult.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_steps_one_tighter_when_the_next_two_page_level_also_fits()
    {
        // Levels 0-2 → 3 pages (over max), level 3 → 2 pages (loosest within max), level 4 → 2 pages.
        var pageCounts = new Dictionary<int, int>
        {
            [0] = 3,
            [1] = 3,
            [2] = 3,
            [3] = 2,
            [4] = 2,
        };
        var htmlBuilder = new RecordingHtmlDocumentBuilder();
        var dispatcher = new ScriptedRenderDispatcher();
        var service = CreateService(htmlBuilder, dispatcher, pageCounts);

        var htmlResult = await service.ExportHtmlAsync(CreateUser());
        var pdfResult = await service.ExportPdfAsync(CreateUser());

        Assert.Equal(4, htmlResult.CompactLevel);
        Assert.Equal(4, htmlBuilder.LastOptions?.CompactLevel);
        Assert.Equal("html-level-4", htmlResult.Html);
        Assert.Equal(2, pdfResult.PageCount);
        Assert.False(pdfResult.ExceedsMaxPages);
        Assert.Equal(
            "Your CV is 2 pages; one page is recommended — consider trimming content.",
            pdfResult.Notice);
        Assert.Equal(pdfResult.Notice, htmlResult.Notice);
    }

    [Fact]
    public async Task Export_falls_back_to_fewest_pages_and_flags_exceeds_max_when_two_pages_unreachable()
    {
        // Never fits two pages; best effort is the first level with the fewest pages (level 2 → 3 pages).
        var pageCounts = new Dictionary<int, int>
        {
            [0] = 5,
            [1] = 4,
            [2] = 3,
            [3] = 3,
            [4] = 3,
        };
        var htmlBuilder = new RecordingHtmlDocumentBuilder();
        var dispatcher = new ScriptedRenderDispatcher();
        var service = CreateService(htmlBuilder, dispatcher, pageCounts);

        var htmlResult = await service.ExportHtmlAsync(CreateUser());
        var pdfResult = await service.ExportPdfAsync(CreateUser());

        Assert.Equal(2, htmlResult.CompactLevel);
        Assert.Equal(2, htmlBuilder.LastOptions?.CompactLevel);
        Assert.Equal(3, pdfResult.PageCount);
        Assert.True(pdfResult.ExceedsMaxPages);
        Assert.Equal(
            "This export is 3 pages after compacting; the maximum is two pages.",
            pdfResult.Notice);
        Assert.Contains("maximum is two pages", htmlResult.Notice, StringComparison.Ordinal);
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

        public Task<CvDocumentDto?> ReplaceUnplacedContentAsync(
            AppUserEntity user,
            IReadOnlyList<string> items,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingHtmlDocumentBuilder : ICvExportHtmlDocumentBuilder
    {
        public int CallCount { get; private set; }
        public CvPdfRenderOptions? LastOptions { get; private set; }

        public Task<string> BuildAsync(
            CvExportRenderRequest request,
            CvPdfRenderOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastOptions = options ?? CvPdfRenderOptions.Normal;
            var level = LastOptions.CompactLevel;
            return Task.FromResult($"html-level-{level}");
        }
    }

    private sealed class ScriptedRenderDispatcher : ICvHtmlCvPdfExporter
    {
        public int CallCount { get; private set; }
        public int LastCompactLevel { get; private set; }

        public Task<byte[]> ExportAsync(
            CvExportRenderRequest request,
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
