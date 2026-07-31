using System.Net;
using System.Text.RegularExpressions;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.HtmlExport;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace ApplyVault.Api.IntegrationTests;

/// <summary>
/// Test double for HTML→PDF that exercises the shared HTML document builder without Chromium.
/// Emits a PdfSharp page containing stripped HTML text so PdfPig content asserts still work.
/// </summary>
public sealed class HtmlCvPdfExporterTestDouble(ICvExportHtmlDocumentBuilder htmlDocumentBuilder) : ICvHtmlCvPdfExporter
{
    public async Task<byte[]> ExportAsync(
        CvExportRenderRequest request,
        int templateId,
        CvPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var html = await htmlDocumentBuilder
            .BuildAsync(request, templateId, options, cancellationToken)
            .ConfigureAwait(false);
        var plainText = StripHtml(html);

        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;

        using (var graphics = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 9);
            var y = 40.0;
            const double lineHeight = 11.0;

            foreach (var wordGroup in ChunkWords(plainText, 18))
            {
                if (y > page.Height.Point - 40)
                {
                    break;
                }

                graphics.DrawString(wordGroup, font, XBrushes.Black, new XPoint(40, y));
                y += lineHeight;
            }
        }

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static IEnumerable<string> ChunkWords(string text, int wordsPerLine)
    {
        var words = text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i += wordsPerLine)
        {
            yield return string.Join(' ', words.Skip(i).Take(wordsPerLine));
        }
    }

    private static string StripHtml(string html)
    {
        var withoutBlocks = Regex.Replace(
            html,
            "<(script|style)[^>]*>.*?</\\1>",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var withoutTags = Regex.Replace(withoutBlocks, "<[^>]+>", " ");
        return WebUtility.HtmlDecode(withoutTags);
    }
}
