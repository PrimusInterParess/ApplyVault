using ApplyVault.Api.Services;
using ApplyVault.Api.Services.HtmlExport;

namespace ApplyVault.Api.Tests;

public sealed class CvExportHtmlMapperLayoutTests
{
    private const string Template = "{{ProfilePhotoHtml}}|H:{{HeaderHtml}}|M:{{MainHtml}}";

    [Fact]
    public void ApplyTemplate_puts_Contact_in_Header_and_Summary_with_the_rest_in_Main()
    {
        var html = CvExportHtmlMapper.ApplyTemplate(Template, BuildFixtureRequest());

        var header = Slice(html, "|H:", "|M:");
        var main = Slice(html, "|M:", null);

        Assert.Contains("Jane Doe", header, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Experienced engineer.", header, StringComparison.Ordinal);
        Assert.DoesNotContain("Acme Corp", header, StringComparison.Ordinal);

        Assert.Contains("Experienced engineer.", main, StringComparison.Ordinal);
        Assert.Contains("""class="section-title">Summary</h2>""", main, StringComparison.Ordinal);
        Assert.Contains("Acme Corp", main, StringComparison.Ordinal);
        Assert.Contains("C#", main, StringComparison.Ordinal);
        Assert.True(
            main.IndexOf("Experienced engineer.", StringComparison.Ordinal)
            < main.IndexOf("Acme Corp", StringComparison.Ordinal));
        Assert.DoesNotContain("Jane Doe", main, StringComparison.Ordinal);
        Assert.DoesNotContain("jane@example.com", main, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("""class="section-title">Contact</h2>""", header, StringComparison.Ordinal);
        Assert.DoesNotContain("""class="section-title">Contact</h2>""", main, StringComparison.Ordinal);
        Assert.DoesNotContain("""class="section-title">Summary</h2>""", header, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyTemplate_emits_contact_value_lines_without_channel_labels()
    {
        var html = CvExportHtmlMapper.ApplyTemplate(Template, BuildFixtureRequest());

        var header = Slice(html, "|H:", "|M:");

        Assert.Contains("""class="cv-contact-line">""", header, StringComparison.Ordinal);
        Assert.DoesNotContain("""class="entry-title">Email</div>""", header, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyTemplate_leaves_no_unresolved_placeholders()
    {
        var html = CvExportHtmlMapper.ApplyTemplate(Template, BuildFixtureRequest());

        Assert.DoesNotContain("{{", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyTemplate_omits_non_contact_sections_that_only_have_a_heading()
    {
        var fixture = BuildFixtureRequest();
        var request = fixture with
        {
            Sections =
            [
                ..fixture.Sections,
                new CvExportSection(
                    "Certifications & Languages",
                    CvSectionTypes.Custom,
                    6,
                    [])
            ]
        };

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request);

        Assert.DoesNotContain("Certifications", html, StringComparison.Ordinal);
        Assert.Contains("Acme Corp", html, StringComparison.Ordinal);
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
