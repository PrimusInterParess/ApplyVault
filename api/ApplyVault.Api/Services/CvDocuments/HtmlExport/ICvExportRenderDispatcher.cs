namespace ApplyVault.Api.Services.HtmlExport;

public interface ICvExportRenderDispatcher
{
    Task<byte[]> RenderAsync(
        CvExportRenderRequest request,
        int templateId,
        CvPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}
