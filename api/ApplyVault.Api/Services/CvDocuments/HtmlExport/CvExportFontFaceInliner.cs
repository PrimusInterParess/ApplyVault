using System.Text.RegularExpressions;

namespace ApplyVault.Api.Services.HtmlExport;

/// <summary>
/// Inlines shipped <c>fonts/*.woff2</c> files as data URIs.
/// Puppeteer <c>setContent</c> stays on <c>about:blank</c>, which cannot read relative font URLs.
/// </summary>
internal static partial class CvExportFontFaceInliner
{
    public static string Inline(string html, string templatesDirectory)
    {
        var fontsDirectory = Path.Combine(templatesDirectory, "fonts");

        return FontUrl().Replace(html, (match) =>
        {
            var fileName = match.Groups[1].Value;
            var path = Path.Combine(fontsDirectory, fileName);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"CV export font was not found: {fileName}", path);
            }

            var base64 = Convert.ToBase64String(File.ReadAllBytes(path));
            return $"url(\"data:font/woff2;base64,{base64}\")";
        });
    }

    [GeneratedRegex(
        """url\((?:["']?)fonts/([A-Za-z0-9._-]+\.woff2)["']?\)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex FontUrl();
}
