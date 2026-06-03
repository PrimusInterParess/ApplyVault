namespace ApplyVault.Api.Services;

public sealed record CvPdfExportOptions(int TemplateId = 1, int? MaxPages = null);

public sealed record CvPdfRenderOptions(int CompactLevel = 0)
{
    public const int MaxCompactLevel = 4;

    public static CvPdfRenderOptions Normal { get; } = new();
}

public sealed record CvPdfExportResult(
    byte[] PdfBytes,
    int PageCount,
    int? MaxPages,
    bool ExceedsMaxPages,
    bool UsedAi,
    string? Notice);

public interface ICvPdfExportRenderer
{
    byte[] Render(CvExportRenderRequest request, CvPdfRenderOptions? options = null);
}

public interface ICvPdfPageCounter
{
    int CountPages(byte[] pdfBytes);
}
