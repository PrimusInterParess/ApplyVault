using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvExportMarkdownParserTests
{
    [Fact]
    public void Parse_recognizes_headings_and_bullets()
    {
        var blocks = CvExportMarkdownParser.Parse(
            """
            ### Software Engineer
            **Acme** · 2020 – 2024

            - Built APIs
            """);

        Assert.Equal(3, blocks.Count);
        Assert.Equal(CvExportMarkdownBlockKind.Heading, blocks[0].Kind);
        Assert.Equal("Software Engineer", blocks[0].Text);
        Assert.Equal(CvExportMarkdownBlockKind.Paragraph, blocks[1].Kind);
        Assert.Equal(CvExportMarkdownBlockKind.Bullet, blocks[2].Kind);
    }

    [Fact]
    public void ParseRuns_splits_bold_segments()
    {
        var runs = CvExportMarkdownParser.ParseRuns("**Acme Corp** · 2020 – 2024");

        Assert.True(runs.Count >= 2);
        Assert.True(runs[0].Bold);
        Assert.Equal("Acme Corp", runs[0].Text);
        Assert.Contains(runs, (run) => !run.Bold && run.Text.Contains("2020", StringComparison.Ordinal));
    }
}
