namespace ApplyVault.Api.Services.HtmlExport;

public sealed class CvExportRenderDispatcher(
    ICvHtmlCvPdfExporter htmlCvPdfExporter) : ICvExportRenderDispatcher
{
    public Task<byte[]> RenderAsync(
        CvExportRenderRequest request,
        int templateId,
        CvPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTemplateId = CvExportHtmlTemplateCatalog.NormalizeTemplateId(templateId);

        // QuestPDF retired from the supported set (Modern/Minimal). EnableHtmlTemplates
        // remains the Chromium startup/ops switch; PDF export always uses HTML.
        return htmlCvPdfExporter.ExportAsync(request, resolvedTemplateId, options, cancellationToken);
    }
}
