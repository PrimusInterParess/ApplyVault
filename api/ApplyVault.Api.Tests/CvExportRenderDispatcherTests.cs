using ApplyVault.Api.Services;
using ApplyVault.Api.Services.HtmlExport;

namespace ApplyVault.Api.Tests;

public sealed class CvExportRenderDispatcherTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    public async Task RenderAsync_routes_all_v1_ids_through_html_exporter(int inputId, int expectedResolvedId)
    {
        var htmlExporter = new RecordingHtmlExporter();
        var dispatcher = new CvExportRenderDispatcher(htmlExporter);
        var request = new CvExportRenderRequest(CvExportLayoutDefaults.Document(), [], null, null);

        await dispatcher.RenderAsync(request, inputId);

        Assert.Equal(1, htmlExporter.CallCount);
        Assert.Equal(expectedResolvedId, htmlExporter.LastTemplateId);
        Assert.Same(request, htmlExporter.LastRequest);
    }

    private sealed class RecordingHtmlExporter : ICvHtmlCvPdfExporter
    {
        public int CallCount { get; private set; }
        public int LastTemplateId { get; private set; }
        public CvExportRenderRequest? LastRequest { get; private set; }

        public Task<byte[]> ExportAsync(
            CvExportRenderRequest request,
            int templateId,
            CvPdfRenderOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastTemplateId = templateId;
            LastRequest = request;
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }
    }
}
