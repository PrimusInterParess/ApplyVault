using System.Text.RegularExpressions;

namespace ApplyVault.Api.Tests;

public sealed class CvExportClassicContactLayoutTests
{
    [Fact]
    public void Classic_template_header_matches_editable_frontend_preview()
    {
        var html = File.ReadAllText(ResolveClassicTemplatePath());
        var rootRule = ExtractCssRule(html, ":root");
        var headerRule = ExtractCssRule(html, ".cv-header");
        var photoRule = ExtractCssRule(html, ".cv-photo");
        var contactRule = ExtractCssRule(html, ".cv-contact");

        // Equal page inset on all sides (one-page baseline).
        Assert.Contains("--cv-page-pad-y: 14mm", rootRule, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--cv-page-pad-x: 14mm", rootRule, StringComparison.OrdinalIgnoreCase);

        // Photo baseline tracks one-page Classic (compact ramp can shrink further).
        Assert.Contains("width: 120px", photoRule, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("height: 120px", photoRule, StringComparison.OrdinalIgnoreCase);

        // FE editable: gap 1rem ≈ 12pt; no rule under contacts.
        Assert.Contains("gap: 12pt", headerRule, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("border-bottom: none", headerRule, StringComparison.OrdinalIgnoreCase);

        // Vertical contact stack with shared spacing token.
        Assert.DoesNotContain("flex-wrap: wrap", contactRule, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("flex-direction: column", contactRule, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gap: var(--cv-space)", contactRule, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractCssRule(string html, string selector)
    {
        var pattern = Regex.Escape(selector) + @"\s*\{([^}]*)\}";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Expected {selector} rule in template-classic.html.");
        return match.Groups[1].Value;
    }

    private static string ResolveClassicTemplatePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "ApplyVault.Api",
                "wwwroot",
                "cv-export-templates",
                "template-classic.html");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(
                dir.FullName,
                "api",
                "ApplyVault.Api",
                "wwwroot",
                "cv-export-templates",
                "template-classic.html");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate api/ApplyVault.Api/wwwroot/cv-export-templates/template-classic.html from test base directory.");
    }
}
