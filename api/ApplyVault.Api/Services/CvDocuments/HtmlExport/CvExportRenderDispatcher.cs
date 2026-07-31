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

        // M1: QuestPDF retired from the v1 supported set (ids 1–3). EnableHtmlTemplates
        // remains the Chromium startup/ops switch; PDF export for 1–3 always uses HTML.
        return htmlCvPdfExporter.ExportAsync(request, resolvedTemplateId, options, cancellationToken);
    }
}
