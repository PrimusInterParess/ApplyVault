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

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ApplyTemplate_single_Phone_appears_once_in_Classic_and_Minimal_header(int templateId)
    {
        const string phone = "+45 12 34 56 78";
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvExportEntry("Name", "Jane Doe", null, string.Empty, [], string.Empty),
                        new CvExportEntry("Phone", null, null, string.Empty, [phone], string.Empty),
                        new CvExportEntry("Email", null, null, string.Empty, ["jane@example.com"], string.Empty)
                    ]),
                new CvExportSection(
                    "Summary",
                    CvSectionTypes.Summary,
                    1,
                    [
                        new CvExportEntry(string.Empty, null, null, "Experienced engineer.", [], string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId);

        Assert.Equal(1, CountOccurrences(html, phone));
        Assert.Contains("""class="cv-contact-line">""", html, StringComparison.Ordinal);
        Assert.DoesNotContain("""class="section-title">Contact</h2>""", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ApplyTemplate_dedupes_identical_Contact_channel_values_case_insensitive(int templateId)
    {
        const string phone = "+45 12 34 56 78";
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvExportEntry("Name", "Jane Doe", null, string.Empty, [], string.Empty),
                        new CvExportEntry("Phone", null, null, string.Empty, [phone], string.Empty),
                        new CvExportEntry("Mobile", null, null, string.Empty, ["  +45 12 34 56 78  "], string.Empty),
                        new CvExportEntry("Phone", null, null, phone.ToUpperInvariant(), [], string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId);

        Assert.Equal(1, CountOccurrences(html, phone, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("""class="section-title">Contact</h2>""", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ApplyTemplate_import_legacy_Contact_emits_name_and_all_distinct_channels(int templateId)
    {
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvExportEntry(
                            "Jane Doe",
                            null,
                            null,
                            string.Empty,
                            ["jane@example.com", "+45 12 34 56 78", "linkedin.com/in/jane"],
                            string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId);

        Assert.Contains("""class="cv-name">Jane Doe</h1>""", html, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+45 12 34 56 78", html, StringComparison.Ordinal);
        Assert.Contains("linkedin.com/in/jane", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, CountOccurrences(html, """class="cv-contact-line">"""));
        Assert.Contains("""class="cv-contact">""", html, StringComparison.Ordinal);
        Assert.DoesNotContain("""class="section-title">Contact</h2>""", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ApplyTemplate_multi_bullet_non_Name_entry_emits_one_line_per_distinct_bullet(int templateId)
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
                            ["jane@example.com", "jane.work@example.com", "jane@example.com"],
                            string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId);

        Assert.Contains("""class="cv-name">Jane Doe</h1>""", html, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jane.work@example.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, CountOccurrences(html, "jane@example.com", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, CountOccurrences(html, """class="cv-contact-line">"""));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ApplyTemplate_Name_entry_bullets_are_promoted_to_contact_lines(int templateId)
    {
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvExportEntry(
                            "Name",
                            "Jane Doe",
                            null,
                            string.Empty,
                            ["jane@example.com", "+45 98 76 54 32"],
                            string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId);

        Assert.Contains("""class="cv-name">Jane Doe</h1>""", html, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+45 98 76 54 32", html, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(html, """class="cv-contact-line">"""));
    }

    private static int CountOccurrences(string haystack, string needle, StringComparison comparison = StringComparison.Ordinal)
    {
        var count = 0;
        var index = 0;

        while ((index = haystack.IndexOf(needle, index, comparison)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
