using PdfSharp.Fonts;

namespace ApplyVault.Api.Services;

internal sealed class ApplyVaultPdfFontResolver : IFontResolver
{
    public static readonly ApplyVaultPdfFontResolver Instance = new();

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        _ = isItalic;

        var faceName = isBold ? "ArialBold" : "Arial";
        return new FontResolverInfo(faceName);
    }

    public byte[]? GetFont(string faceName)
    {
        var fileName = faceName switch
        {
            "ArialBold" => "arialbd.ttf",
            _ => "arial.ttf"
        };

        var fontPath = FindFontFile(fileName);

        return fontPath is null ? null : File.ReadAllBytes(fontPath);
    }

    private static string? FindFontFile(string fileName)
    {
        foreach (var directory in GetFontDirectories())
        {
            var candidate = Path.Combine(directory, fileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetFontDirectories()
    {
        if (OperatingSystem.IsWindows())
        {
            var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            if (!string.IsNullOrWhiteSpace(windowsDirectory))
            {
                yield return windowsDirectory;
            }
        }

        yield return "/usr/share/fonts/truetype/liberation";
        yield return "/usr/share/fonts/truetype/dejavu";
        yield return "/usr/share/fonts/TTF";
    }
}
