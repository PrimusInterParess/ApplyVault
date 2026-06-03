using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.HtmlExport;

public sealed class CvExportRenderDispatcher(
    ICvPdfExportRenderer questPdfRenderer,
    ICvHtmlCvPdfExporter htmlCvPdfExporter,
    IOptions<CvHtmlExportOptions> htmlExportOptions) : ICvExportRenderDispatcher
{
    public Task<byte[]> RenderAsync(
        CvExportRenderRequest request,
        int templateId,
        CvPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (templateId == 1 || !CvExportHtmlTemplateCatalog.UsesHtmlRenderer(templateId))
        {
            return Task.FromResult(questPdfRenderer.Render(request, options));
        }

        if (!htmlExportOptions.Value.EnableHtmlTemplates)
        {
            return Task.FromResult(questPdfRenderer.Render(request, options));
        }

        return htmlCvPdfExporter.ExportAsync(request, templateId, options, cancellationToken);
    }
}
