using ApplyVault.Api.Services.Shared;

namespace ApplyVault.Api.Tests;

public sealed class JobDescriptionHtmlSanitizerTests
{
    [Fact]
    public void Sanitize_PreservesAllowedTags()
    {
        var input = "<p>Build <strong>APIs</strong></p><ul><li>C#</li></ul>";

        var sanitized = JobDescriptionHtmlSanitizer.Sanitize(input);

        Assert.Contains("<p>", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<strong>", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<ul>", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<li>", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_RemovesScriptAndEventHandlers()
    {
        var input = "<p onclick=\"steal()\">Safe</p><script>alert(1)</script>";

        var sanitized = JobDescriptionHtmlSanitizer.Sanitize(input);

        Assert.Contains("Safe", sanitized);
        Assert.DoesNotContain("script", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_AllowsSafeLinks()
    {
        var input = """<p>Apply at <a href="https://jobs.example.com/apply">company site</a></p>""";

        var sanitized = JobDescriptionHtmlSanitizer.Sanitize(input);

        Assert.Contains("href=", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://jobs.example.com/apply", sanitized);
    }

    [Fact]
    public void Sanitize_RemovesJavascriptLinks()
    {
        var input = """<a href="javascript:alert(1)">Click</a>""";

        var sanitized = JobDescriptionHtmlSanitizer.Sanitize(input);

        Assert.DoesNotContain("javascript:", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_ReturnsEmptyForBlankInput()
    {
        Assert.Equal(string.Empty, JobDescriptionHtmlSanitizer.Sanitize(null));
        Assert.Equal(string.Empty, JobDescriptionHtmlSanitizer.Sanitize("   "));
    }
}
