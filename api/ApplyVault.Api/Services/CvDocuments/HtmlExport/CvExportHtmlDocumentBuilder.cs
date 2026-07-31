using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.HtmlExport;

public interface ICvExportHtmlDocumentBuilder
{
    Task<string> BuildAsync(
        CvExportRenderRequest request,
        int templateId,
        CvPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed class CvExportHtmlDocumentBuilder(
    IWebHostEnvironment environment,
    IOptions<CvHtmlExportOptions> options) : ICvExportHtmlDocumentBuilder
{
    public async Task<string> BuildAsync(
        CvExportRenderRequest request,
        int templateId,
        CvPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTemplateId = CvExportHtmlTemplateCatalog.NormalizeTemplateId(templateId);
        var templateFileName = CvExportHtmlTemplateCatalog.GetHtmlTemplateFileName(resolvedTemplateId)
            ?? throw new InvalidOperationException($"HTML template {resolvedTemplateId} is not configured.");

        var templatePath = ResolveTemplatePath(templateFileName);

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"CV HTML template was not found: {templateFileName}", templatePath);
        }

        var templateHtml = await File.ReadAllTextAsync(templatePath, cancellationToken).ConfigureAwait(false);
        return InjectPrintStyles(
            CvExportHtmlMapper.ApplyTemplate(templateHtml, request, resolvedTemplateId),
            options);
    }

    private string ResolveTemplatePath(string templateFileName) =>
        Path.Combine(ResolveTemplatesDirectory(), templateFileName);

    private string ResolveTemplatesDirectory()
    {
        var subfolder = options.Value.TemplatesSubfolder.Trim().Trim('/');
        var webRoot = environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        return Path.Combine(webRoot, subfolder);
    }

    private string InjectPrintStyles(string html, CvPdfRenderOptions? renderOptions)
    {
        var printCssPath = Path.Combine(ResolveTemplatesDirectory(), "cv-export-print.css");

        if (!File.Exists(printCssPath))
        {
            return html;
        }

        var printCss = File.ReadAllText(printCssPath);
        var compactCss = CvExportCompactCssBuilder.Build(renderOptions);

        const string headClose = "</head>";

        if (!html.Contains(headClose, StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        return html.Replace(
            headClose,
            $"<style>{printCss}{compactCss}</style>{headClose}",
            StringComparison.OrdinalIgnoreCase);
    }
}
