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

    [Theory]
    [InlineData(3)]
    public void ApplyTemplate_emits_cv_name_heading_before_contact_lines(int templateId)
    {
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvExportEntry("Email", null, null, string.Empty, ["jane@example.com"], string.Empty),
                        new CvExportEntry("Name", "Jane Doe", null, string.Empty, [], string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId);
        var nameIndex = html.IndexOf("""class="cv-name">Jane Doe</h1>""", StringComparison.Ordinal);
        var contactIndex = html.IndexOf("""class="cv-contact-line">""", StringComparison.Ordinal);

        Assert.True(nameIndex >= 0, "Expected cv-name heading with the contact display name.");
        Assert.True(contactIndex >= 0, "Expected contact channel lines.");
        Assert.True(nameIndex < contactIndex, "Display name must appear before contact lines.");
    }

    [Theory]
    [InlineData(3)]
    public void ApplyTemplate_legacy_person_title_fills_cv_name_gap(int templateId)
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
                            ["jane@example.com", "+45 12 34 56 78"],
                            string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId);

        Assert.Contains("""class="cv-name">Jane Doe</h1>""", html, StringComparison.Ordinal);
        Assert.DoesNotContain("""class="cv-name">Name</h1>""", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(3)]
    public void ApplyTemplate_display_name_is_not_repeated_as_contact_line(int templateId)
    {
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvExportEntry("Name", "Jane Doe", null, string.Empty, ["Jane Doe"], string.Empty),
                        new CvExportEntry("Email", null, null, string.Empty, ["jane@example.com"], string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId);

        Assert.Equal(1, CountOccurrences(html, """class="cv-name">Jane Doe</h1>"""));
        Assert.DoesNotContain("""class="cv-contact-line">Jane Doe</p>""", html, StringComparison.Ordinal);
        Assert.Contains("""class="cv-contact-line">jane@example.com</p>""", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ApplyTemplate_display_name_not_repeated_via_separate_unlabeled_channel(int templateId)
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
                        // Leftover import/orphan line equal to the display name.
                        new CvExportEntry(string.Empty, null, null, string.Empty, ["Jane Doe"], string.Empty),
                        new CvExportEntry("Email", null, null, string.Empty, ["jane@example.com"], string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId);

        Assert.Equal(1, CountOccurrences(html, "Jane Doe", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("""class="cv-contact-line">Jane Doe</p>""", html, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ApplyTemplate_multiple_Contact_typed_sections_emit_display_name_once(int templateId)
    {
        // Real import shape: Contact + Profile both typed Contact, each with a Name row.
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvExportEntry("Name", "Karolina Borisova", null, string.Empty, [], string.Empty)
                    ]),
                new CvExportSection(
                    "Profile",
                    CvSectionTypes.Contact,
                    1,
                    [
                        new CvExportEntry("Name", "KAROLINA BORISOVA", null, string.Empty, [], string.Empty),
                        new CvExportEntry(string.Empty, null, null, string.Empty, ["Phone: +359 88 348 3311"], string.Empty),
                        new CvExportEntry(string.Empty, null, null, string.Empty, ["kborisova171921@gmail.com"], string.Empty),
                        new CvExportEntry(string.Empty, null, null, string.Empty, ["linkedin.com/in/karolina-borisova-dev"], string.Empty)
                    ])
            ],
            null,
            null);

        var html = CvExportHtmlMapper.ApplyTemplate(Template, request, templateId);

        Assert.Equal(1, CountOccurrences(html, """class="cv-name">""", StringComparison.Ordinal));
        Assert.Equal(1, CountOccurrences(html, "Karolina Borisova", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("kborisova171921@gmail.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+359 88 348 3311", html, StringComparison.Ordinal);
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
    [InlineData(3)]
    public void ApplyTemplate_single_Phone_appears_once_in_Minimal_header(int templateId)
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
