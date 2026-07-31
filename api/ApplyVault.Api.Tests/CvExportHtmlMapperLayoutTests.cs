using ApplyVault.Api.Services;
using ApplyVault.Api.Services.HtmlExport;

namespace ApplyVault.Api.Tests;

public sealed class CvExportHtmlMapperLayoutTests
{
    private const string Template = "{{ProfilePhotoHtml}}|H:{{HeaderHtml}}|S:{{SidebarHtml}}|M:{{MainHtml}}";

    [Fact]
    public void ApplyTemplate_Classic_puts_Contact_and_Summary_in_Header()
    {
        var html = CvExportHtmlMapper.ApplyTemplate(Template, BuildFixtureRequest(), templateId: 1);

        Assert.Contains("|H:", html, StringComparison.Ordinal);
        Assert.Contains("Jane Doe", html, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Experienced engineer.", html, StringComparison.Ordinal);

        var header = Slice(html, "|H:", "|S:");
        var sidebar = Slice(html, "|S:", "|M:");
        var main = Slice(html, "|M:", null);

        Assert.Contains("Jane Doe", header, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Experienced engineer.", header, StringComparison.Ordinal);
        Assert.DoesNotContain("Acme Corp", header, StringComparison.Ordinal);

        Assert.True(string.IsNullOrWhiteSpace(sidebar));
        Assert.Contains("Acme Corp", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Jane Doe", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Experienced engineer.", main, StringComparison.Ordinal);
        Assert.DoesNotContain("""class="section-title">Contact</h2>""", header, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyTemplate_Modern_puts_Skills_Summary_Contact_in_Sidebar()
    {
        var html = CvExportHtmlMapper.ApplyTemplate(Template, BuildFixtureRequest(), templateId: 2);

        var header = Slice(html, "|H:", "|S:");
        var sidebar = Slice(html, "|S:", "|M:");
        var main = Slice(html, "|M:", null);

        Assert.True(string.IsNullOrWhiteSpace(header));
        Assert.Contains("Jane Doe", sidebar, StringComparison.Ordinal);
        Assert.Contains("Experienced engineer.", sidebar, StringComparison.Ordinal);
        Assert.Contains("C#", sidebar, StringComparison.Ordinal);
        Assert.Contains("Acme Corp", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Acme Corp", sidebar, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyTemplate_Minimal_puts_Contact_and_Summary_in_Header()
    {
        var html = CvExportHtmlMapper.ApplyTemplate(Template, BuildFixtureRequest(), templateId: 3);

        var header = Slice(html, "|H:", "|S:");
        var sidebar = Slice(html, "|S:", "|M:");
        var main = Slice(html, "|M:", null);

        Assert.Contains("Jane Doe", header, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Experienced engineer.", header, StringComparison.Ordinal);
        Assert.DoesNotContain("Acme Corp", header, StringComparison.Ordinal);

        Assert.True(string.IsNullOrWhiteSpace(sidebar));
        Assert.Contains("Acme Corp", main, StringComparison.Ordinal);
        Assert.Contains("C#", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Jane Doe", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Experienced engineer.", main, StringComparison.Ordinal);
        Assert.DoesNotContain("jane@example.com", main, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("""class="section-title">Contact</h2>""", header, StringComparison.Ordinal);
        Assert.DoesNotContain("""class="section-title">Contact</h2>""", main, StringComparison.Ordinal);
        Assert.DoesNotContain("""class="section-title">Summary</h2>""", main, StringComparison.Ordinal);
    }

    private static CvExportRenderRequest BuildFixtureRequest() =>
        new(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvExportEntry("Name", "Jane Doe", null, string.Empty, [], string.Empty),
                        new CvExportEntry("Email", null, null, string.Empty, ["jane@example.com"], string.Empty)
                    ]),
                new CvExportSection(
                    "Summary",
                    CvSectionTypes.Summary,
                    1,
                    [
                        new CvExportEntry(string.Empty, null, null, "Experienced engineer.", [], string.Empty)
                    ]),
                new CvExportSection(
                    "Skills",
                    CvSectionTypes.Skills,
                    2,
                    [
                        new CvExportEntry(string.Empty, null, null, string.Empty, [], "C#, ASP.NET")
                    ]),
                new CvExportSection(
                    "Experience",
                    CvSectionTypes.Experience,
                    3,
                    [
                        new CvExportEntry("Engineer", "Acme Corp", "2020-2024", "Built APIs.", [], "C#")
                    ])
            ],
            null,
            null);

    private static string Slice(string html, string startMarker, string? endMarker)
    {
        var start = html.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += startMarker.Length;

        if (endMarker is null)
        {
            return html[start..];
        }

        var end = html.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end >= 0);
        return html[start..end];
    }
}
