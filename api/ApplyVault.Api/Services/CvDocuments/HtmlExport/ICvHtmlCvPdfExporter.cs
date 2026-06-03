namespace ApplyVault.Api.Services.HtmlExport;

public interface ICvHtmlCvPdfExporter
{
    Task<byte[]> ExportAsync(
        CvExportRenderRequest request,
        int templateId,
        CvPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}
