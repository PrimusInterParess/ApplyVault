namespace ApplyVault.Api.Services;

public sealed record CvPdfExportResult(byte[] PdfBytes, bool UsedAi, string? Notice);

public interface ICvPdfExportRenderer
{
    byte[] Render(CvExportRenderRequest request);
}
