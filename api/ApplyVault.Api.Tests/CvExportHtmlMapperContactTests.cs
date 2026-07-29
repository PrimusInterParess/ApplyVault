using ApplyVault.Api.Services;
using ApplyVault.Api.Services.HtmlExport;

namespace ApplyVault.Api.Tests;

public sealed class CvExportHtmlMapperContactTests
{
    private const string Template = "{{ProfilePhotoHtml}}{{HeaderHtml}}{{SidebarHtml}}{{MainHtml}}";

    [Fact]
    public void ApplyTemplate_omits_labeled_empty_Contact_channels()
    {
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvExportEntry("Name", null, null, string.Empty, [], string.Empty),
                        new CvExportEntry("Email", null, null, string.Empty, [], string.Empty),
                        new CvExportEntry("Phone", null, null, string.Empty, [], string.Empty),
                        new CvExportEntry("LinkedIn", null, null, string.Empty, [], string.Empty)
                    ]),
                new CvExportSection(
                    "Summary",
                    CvSectionTypes.Summary,
                    1,
                    [
                        new CvExportEntry(
                            string.Empty,
                            null,
                            null,
                            "Experienced engineer.",
                            [],
                            string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId: 3);

        Assert.Contains("Experienced", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Email", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Phone", html, StringComparison.Ordinal);
        Assert.DoesNotContain("LinkedIn", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Name<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("""class="section-title">Contact</h2>""", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyTemplate_keeps_Contact_name_subtitle_and_valued_channels()
    {
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvExportEntry("Name", "Jane Doe", null, string.Empty, [], string.Empty),
                        new CvExportEntry(
                            "Email",
                            null,
                            null,
                            string.Empty,
                            ["jane@example.com"],
                            string.Empty),
                        new CvExportEntry("Phone", null, null, string.Empty, [], string.Empty),
                        new CvExportEntry("LinkedIn", null, null, string.Empty, [], string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId: 3);

        Assert.Contains("Jane Doe", html, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Phone", html, StringComparison.Ordinal);
        Assert.DoesNotContain("LinkedIn", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyTemplate_omits_Contact_channels_with_whitespace_only_values()
    {
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvExportEntry("Name", "   ", null, string.Empty, [], string.Empty),
                        new CvExportEntry("Email", null, null, "   ", ["  ", "\t"], string.Empty),
                        new CvExportEntry("Phone", null, null, string.Empty, ["   "], string.Empty)
                    ]),
                new CvExportSection(
                    "Summary",
                    CvSectionTypes.Summary,
                    1,
                    [
                        new CvExportEntry(
                            string.Empty,
                            null,
                            null,
                            "Keep this summary.",
                            [],
                            string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId: 3);

        Assert.Contains("Keep this summary.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Email", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Phone", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Name<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("""class="section-title">Contact</h2>""", html, StringComparison.Ordinal);
    }
}
