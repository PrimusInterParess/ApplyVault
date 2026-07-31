using System.Net;
using System.Text;

namespace ApplyVault.Api.Services.HtmlExport;

internal static class CvExportHtmlMapper
{
    public static string ApplyTemplate(
        string templateHtml,
        CvExportRenderRequest request,
        int templateId)
    {
        var orderedSections = request.Sections
            .OrderBy((section) => section.SortOrder)
            .ToArray();

        var profilePhotoHtml = BuildProfilePhotoHtml(request.ProfilePhotoBytes, request.ProfilePhotoContentType);
        var (headerHtml, sidebarHtml, mainHtml) = BuildColumnContent(orderedSections, templateId);

        return templateHtml
            .Replace("{{ProfilePhotoHtml}}", profilePhotoHtml, StringComparison.Ordinal)
            .Replace("{{HeaderHtml}}", headerHtml, StringComparison.Ordinal)
            .Replace("{{SidebarHtml}}", sidebarHtml, StringComparison.Ordinal)
            .Replace("{{MainHtml}}", mainHtml, StringComparison.Ordinal);
    }

    private static string BuildProfilePhotoHtml(byte[]? photoBytes, string? contentType)
    {
        if (photoBytes is not { Length: > 0 })
        {
            return string.Empty;
        }

        var mime = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType.Trim();
        var base64 = Convert.ToBase64String(photoBytes);

        return $"""<img class="cv-photo" src="data:{Encode(mime)};base64,{base64}" alt="Profile photo" />""";
    }

    private static (string HeaderHtml, string SidebarHtml, string MainHtml) BuildColumnContent(
        IReadOnlyList<CvExportSection> sections,
        int templateId)
    {
        var resolvedTemplateId = CvExportHtmlTemplateCatalog.NormalizeTemplateId(templateId);

        if (resolvedTemplateId == 1)
        {
            var header = new StringBuilder();
            var main = new StringBuilder();

            foreach (var section in sections.Where(SectionHasContent))
            {
                if (IsClassicHeaderSection(section))
                {
                    if (IsContactSection(section))
                    {
                        AppendClassicContactHeader(header, section);
                    }
                    else
                    {
                        AppendSection(header, section, compact: true);
                    }
                }
                else
                {
                    AppendSection(main, section, compact: false);
                }
            }

            return (header.ToString(), string.Empty, main.ToString());
        }

        if (resolvedTemplateId == 3)
        {
            var body = new StringBuilder();
            AppendSections(body, sections, compact: false);
            return (string.Empty, string.Empty, body.ToString());
        }

        var sidebar = new StringBuilder();
        var mainColumn = new StringBuilder();

        foreach (var section in sections.Where(SectionHasContent))
        {
            if (IsSidebarSection(section))
            {
                AppendSection(sidebar, section, compact: true);
            }
            else
            {
                AppendSection(mainColumn, section, compact: false);
            }
        }

        return (string.Empty, sidebar.ToString(), mainColumn.ToString());
    }

    private static void AppendSections(StringBuilder builder, IEnumerable<CvExportSection> sections, bool compact)
    {
        foreach (var section in sections.Where(SectionHasContent))
        {
            AppendSection(builder, section, compact);
        }
    }

    private static void AppendClassicContactHeader(StringBuilder builder, CvExportSection section)
    {
        foreach (var entry in section.Entries.Where((entry) => EntryHasContent(section, entry)))
        {
            if (IsContactNameEntry(entry))
            {
                if (!string.IsNullOrWhiteSpace(entry.Subtitle))
                {
                    builder.Append($"""<h1 class="cv-name">{RenderInline(entry.Subtitle)}</h1>""");
                }

                continue;
            }

            var value = ContactEntryValue(entry);

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            builder.Append($"""<p class="cv-contact-line">{RenderInline(value)}</p>""");
        }
    }

    private static string ContactEntryValue(CvExportEntry entry)
    {
        var fromBullet = entry.Bullets.FirstOrDefault((line) => !string.IsNullOrWhiteSpace(line));

        if (!string.IsNullOrWhiteSpace(fromBullet))
        {
            return fromBullet.Trim();
        }

        return string.IsNullOrWhiteSpace(entry.Summary) ? string.Empty : entry.Summary.Trim();
    }

    private static void AppendSection(StringBuilder builder, CvExportSection section, bool compact)
    {
        var sectionClass = $"section section-{NormalizeSectionClass(section.SectionType)}";

        builder.Append($"""<section class="{sectionClass}">""");

        if (!string.IsNullOrWhiteSpace(section.Heading))
        {
            builder.Append($"""<h2 class="section-title">{Encode(section.Heading)}</h2>""");
        }

        foreach (var entry in section.Entries.Where((entry) => EntryHasContent(section, entry)))
        {
            AppendEntry(builder, entry, section.SectionType, compact);
        }

        builder.Append("</section>");
    }

    private static void AppendEntry(
        StringBuilder builder,
        CvExportEntry entry,
        string sectionType,
        bool compact)
    {
        builder.Append("""<div class="entry">""");

        if (!string.IsNullOrWhiteSpace(entry.Title) || !string.IsNullOrWhiteSpace(entry.DateRange))
        {
            builder.Append("""<div class="entry-header">""");

            if (!string.IsNullOrWhiteSpace(entry.Title))
            {
                builder.Append($"""<div class="entry-title">{RenderInline(entry.Title)}</div>""");
            }

            if (!string.IsNullOrWhiteSpace(entry.DateRange))
            {
                builder.Append($"""<div class="entry-date">{Encode(entry.DateRange)}</div>""");
            }

            builder.Append("</div>");
        }

        if (!string.IsNullOrWhiteSpace(entry.Subtitle))
        {
            builder.Append($"""<div class="entry-subtitle">{RenderInline(entry.Subtitle)}</div>""");
        }

        if (!string.IsNullOrWhiteSpace(entry.Summary))
        {
            var tag = compact ? "p" : "p";
            builder.Append($"""<{tag} class="entry-summary">{RenderInline(entry.Summary)}</{tag}>""");
        }

        AppendEntryBodyContent(builder, entry, sectionType);
        builder.Append("</div>");
    }

    private static void AppendEntryBodyContent(
        StringBuilder builder,
        CvExportEntry entry,
        string sectionType)
    {
        if (sectionType == CvSectionTypes.Skills)
        {
            var skillItems = GetSkillItems(entry);

            if (skillItems.Count > 0)
            {
                builder.Append($"""<p class="entry-tech">{Encode(string.Join(", ", skillItems))}</p>""");
            }

            return;
        }

        if (entry.Bullets.Count > 0)
        {
            builder.Append("""<ul class="entry-bullets">""");

            foreach (var bullet in entry.Bullets)
            {
                builder.Append($"""<li>{RenderInline(bullet)}</li>""");
            }

            builder.Append("</ul>");
        }

        var techItems = CvExportTextNormalizer.TechItems(entry.TechStack);

        if (techItems.Count > 0)
        {
            builder.Append($"""<p class="entry-tech"><strong>Technologies:</strong> {Encode(string.Join(", ", techItems))}</p>""");
        }
    }

    private static IReadOnlyList<string> GetSkillItems(CvExportEntry entry)
    {
        var fromTechStack = CvExportTextNormalizer.TechItems(entry.TechStack);

        if (fromTechStack.Count > 0)
        {
            return fromTechStack;
        }

        return entry.Bullets;
    }

    private static bool IsContactSection(CvExportSection section) =>
        section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase)
        || section.SectionType == CvSectionTypes.Contact;

    private static bool IsClassicHeaderSection(CvExportSection section) =>
        section.SectionType == CvSectionTypes.Summary || IsContactSection(section);

    private static bool IsContactNameEntry(CvExportEntry entry) =>
        entry.Title.Equals("Name", StringComparison.OrdinalIgnoreCase);

    private static bool IsSidebarSection(CvExportSection section) =>
        section.SectionType == CvSectionTypes.Skills
        || section.SectionType == CvSectionTypes.Summary
        || IsContactSection(section);

    private static bool SectionHasContent(CvExportSection section) =>
        IsContactSection(section)
            ? section.Entries.Any((entry) => ContactEntryHasContent(entry))
            : section.Entries.Any((entry) => EntryHasContent(section, entry))
                || !string.IsNullOrWhiteSpace(section.Heading);

    private static bool EntryHasContent(CvExportSection section, CvExportEntry entry) =>
        IsContactSection(section)
            ? ContactEntryHasContent(entry)
            : GenericEntryHasContent(entry);

    private static bool ContactEntryHasContent(CvExportEntry entry)
    {
        if (IsContactNameEntry(entry))
        {
            return !string.IsNullOrWhiteSpace(entry.Subtitle);
        }

        return !string.IsNullOrWhiteSpace(entry.Subtitle)
            || !string.IsNullOrWhiteSpace(entry.DateRange)
            || !string.IsNullOrWhiteSpace(entry.Summary)
            || entry.Bullets.Any((bullet) => !string.IsNullOrWhiteSpace(bullet))
            || !string.IsNullOrWhiteSpace(entry.TechStack);
    }

    private static bool GenericEntryHasContent(CvExportEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.Title)
        || !string.IsNullOrWhiteSpace(entry.Subtitle)
        || !string.IsNullOrWhiteSpace(entry.DateRange)
        || !string.IsNullOrWhiteSpace(entry.Summary)
        || entry.Bullets.Count > 0
        || !string.IsNullOrWhiteSpace(entry.TechStack);

    private static string NormalizeSectionClass(string sectionType) =>
        string.IsNullOrWhiteSpace(sectionType)
            ? "custom"
            : sectionType.Trim().ToLowerInvariant();

    private static string RenderInline(string? value) =>
        CvExportInlineHtmlRenderer.Render(value);

    private static string Encode(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
