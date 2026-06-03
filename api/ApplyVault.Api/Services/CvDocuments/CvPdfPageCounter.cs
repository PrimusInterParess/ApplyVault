using UglyToad.PdfPig;

namespace ApplyVault.Api.Services;

public sealed class CvPdfPageCounter : ICvPdfPageCounter
{
    public int CountPages(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);
        return document.NumberOfPages;
    }
}
