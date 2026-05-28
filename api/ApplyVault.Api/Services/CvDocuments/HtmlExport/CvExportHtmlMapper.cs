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
        if (templateId == 3)
        {
            var body = new StringBuilder();
            AppendSections(body, sections, compact: false);
            return (string.Empty, string.Empty, body.ToString());
        }

        if (templateId == 5)
        {
            var header = new StringBuilder();
            var body = new StringBuilder();

            foreach (var section in sections.Where(SectionHasContent))
            {
                if (IsContactSection(section))
                {
                    AppendContactHeader(header, section);
                }
                else
                {
                    AppendProfessionalSection(body, section);
                }
            }

            return (header.ToString(), string.Empty, body.ToString());
        }

        var sidebar = new StringBuilder();
        var main = new StringBuilder();

        foreach (var section in sections.Where(SectionHasContent))
        {
            if (IsSidebarSection(section))
            {
                AppendSection(sidebar, section, compact: true);
            }
            else
            {
                AppendSection(main, section, compact: false);
            }
        }

        return (string.Empty, sidebar.ToString(), main.ToString());
    }

    private static void AppendSections(StringBuilder builder, IEnumerable<CvExportSection> sections, bool compact)
    {
        foreach (var section in sections.Where(SectionHasContent))
        {
            AppendSection(builder, section, compact);
        }
    }

    private static void AppendContactHeader(StringBuilder builder, CvExportSection section)
    {
        foreach (var entry in section.Entries.Where(EntryHasContent))
        {
            if (!string.IsNullOrWhiteSpace(entry.Title))
            {
                builder.Append($"""<h1 class="cv-name">{Encode(entry.Title)}</h1>""");
            }

            if (!string.IsNullOrWhiteSpace(entry.Subtitle))
            {
                builder.Append($"""<p class="cv-tagline">{Encode(entry.Subtitle)}</p>""");
            }

            var contactLines = entry.Bullets.Count > 0
                ? entry.Bullets
                : CvExportTextNormalizer.Paragraphs(entry.Summary);

            if (contactLines.Count > 0)
            {
                builder.Append("""<div class="cv-contact">""");

                foreach (var line in contactLines)
                {
                    builder.Append($"""<p class="cv-contact-line">{Encode(line)}</p>""");
                }

                builder.Append("</div>");
            }
        }
    }

    private static void AppendProfessionalSection(StringBuilder builder, CvExportSection section)
    {
        var sectionClass = $"section section-{NormalizeSectionClass(section.SectionType)}";

        builder.Append($"""<section class="{sectionClass}">""");

        if (!string.IsNullOrWhiteSpace(section.Heading))
        {
            builder.Append($"""<h2 class="section-title">{Encode(section.Heading)}</h2>""");
        }

        foreach (var entry in section.Entries.Where(EntryHasContent))
        {
            AppendProfessionalEntry(builder, entry, section.SectionType);
        }

        builder.Append("</section>");
    }

    private static void AppendProfessionalEntry(
        StringBuilder builder,
        CvExportEntry entry,
        string sectionType)
    {
        builder.Append("""<div class="entry">""");

        var titleLine = BuildProfessionalTitleLine(entry);

        if (!string.IsNullOrWhiteSpace(titleLine))
        {
            builder.Append($"""<div class="entry-title-line">{Encode(titleLine)}</div>""");
        }

        if (!string.IsNullOrWhiteSpace(entry.DateRange))
        {
            builder.Append($"""<div class="entry-date">{Encode(entry.DateRange)}</div>""");
        }

        if (!string.IsNullOrWhiteSpace(entry.Summary))
        {
            builder.Append($"""<p class="entry-summary">{Encode(entry.Summary)}</p>""");
        }

        var bullets = GetDisplayBullets(entry, sectionType);

        if (bullets.Count > 0)
        {
            builder.Append("""<ul class="entry-bullets">""");

            foreach (var bullet in bullets)
            {
                builder.Append($"""<li>{Encode(bullet)}</li>""");
            }

            builder.Append("</ul>");
        }

        var techItems = sectionType == CvSectionTypes.Skills
            ? []
            : CvExportTextNormalizer.TechItems(entry.TechStack);

        if (techItems.Count > 0)
        {
            builder.Append($"""<p class="entry-tech"><strong>Technologies:</strong> {Encode(string.Join(", ", techItems))}</p>""");
        }

        builder.Append("</div>");
    }

    private static string? BuildProfessionalTitleLine(CvExportEntry entry)
    {
        var hasTitle = !string.IsNullOrWhiteSpace(entry.Title);
        var hasSubtitle = !string.IsNullOrWhiteSpace(entry.Subtitle);

        if (hasTitle && hasSubtitle)
        {
            return $"{entry.Title} | {entry.Subtitle}";
        }

        if (hasTitle)
        {
            return entry.Title;
        }

        return hasSubtitle ? entry.Subtitle : null;
    }

    private static void AppendSection(StringBuilder builder, CvExportSection section, bool compact)
    {
        var sectionClass = $"section section-{NormalizeSectionClass(section.SectionType)}";

        builder.Append($"""<section class="{sectionClass}">""");

        if (!string.IsNullOrWhiteSpace(section.Heading))
        {
            builder.Append($"""<h2 class="section-title">{Encode(section.Heading)}</h2>""");
        }

        foreach (var entry in section.Entries.Where(EntryHasContent))
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
                builder.Append($"""<div class="entry-title">{Encode(entry.Title)}</div>""");
            }

            if (!string.IsNullOrWhiteSpace(entry.DateRange))
            {
                builder.Append($"""<div class="entry-date">{Encode(entry.DateRange)}</div>""");
            }

            builder.Append("</div>");
        }

        if (!string.IsNullOrWhiteSpace(entry.Subtitle))
        {
            builder.Append($"""<div class="entry-subtitle">{Encode(entry.Subtitle)}</div>""");
        }

        if (!string.IsNullOrWhiteSpace(entry.Summary))
        {
            var tag = compact ? "p" : "p";
            builder.Append($"""<{tag} class="entry-summary">{Encode(entry.Summary)}</{tag}>""");
        }

        var bullets = GetDisplayBullets(entry, sectionType);

        if (bullets.Count > 0)
        {
            builder.Append("""<ul class="entry-bullets">""");

            foreach (var bullet in bullets)
            {
                builder.Append($"""<li>{Encode(bullet)}</li>""");
            }

            builder.Append("</ul>");
        }

        var techItems = sectionType == CvSectionTypes.Skills
            ? []
            : CvExportTextNormalizer.TechItems(entry.TechStack);

        if (techItems.Count > 0)
        {
            builder.Append($"""<p class="entry-tech"><strong>Technologies:</strong> {Encode(string.Join(", ", techItems))}</p>""");
        }

        builder.Append("</div>");
    }

    private static IReadOnlyList<string> GetDisplayBullets(CvExportEntry entry, string sectionType)
    {
        if (entry.Bullets.Count > 0)
        {
            return entry.Bullets;
        }

        return sectionType == CvSectionTypes.Skills
            ? CvExportTextNormalizer.TechItems(entry.TechStack)
            : [];
    }

    private static bool IsContactSection(CvExportSection section) =>
        section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase);

    private static bool IsSidebarSection(CvExportSection section) =>
        section.SectionType == CvSectionTypes.Skills
        || section.SectionType == CvSectionTypes.Summary
        || IsContactSection(section);

    private static bool SectionHasContent(CvExportSection section) =>
        section.Entries.Any(EntryHasContent) || !string.IsNullOrWhiteSpace(section.Heading);

    private static bool EntryHasContent(CvExportEntry entry) =>
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

    private static string Encode(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
