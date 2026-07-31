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

        var sidebar = new StringBuilder();
        var mainColumn = new StringBuilder();

        foreach (var section in sections.Where(SectionHasContent))
        {
            if (IsSidebarSection(section))
            {
                // Match Classic/Minimal contact emission: name + value lines (no Email/Phone labels).
                if (IsContactSection(section))
                {
                    AppendClassicContactHeader(sidebar, section);
                }
                else
                {
                    AppendSection(sidebar, section, compact: true);
                }
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
        var seenContactValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contactLines = new StringBuilder();

        foreach (var entry in NormalizeContactEntriesForExport(section))
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

            // Dedupe identical channel values (case-insensitive trim) so Minimal/Classic
            // headers emit each phone/email/etc. string once even if entries duplicate.
            if (!seenContactValues.Add(value))
            {
                continue;
            }

            contactLines.Append($"""<p class="cv-contact-line">{RenderInline(value)}</p>""");
        }

        if (contactLines.Length > 0)
        {
            builder.Append("""<div class="cv-contact">""");
            builder.Append(contactLines);
            builder.Append("</div>");
        }
    }

    /// <summary>
    /// Expands import-legacy / multi-bullet Contact shapes into Name + one channel entry
    /// per distinct valued bullet for Classic/Minimal header emission.
    /// </summary>
    private static IReadOnlyList<CvExportEntry> NormalizeContactEntriesForExport(CvExportSection section)
    {
        var sourceEntries = section.Entries
            .Where((entry) => EntryHasContent(section, entry))
            .ToArray();

        if (sourceEntries.Length == 0)
        {
            return sourceEntries;
        }

        if (TryExpandImportLegacyContact(sourceEntries, out var legacyExpanded))
        {
            return legacyExpanded;
        }

        var normalized = new List<CvExportEntry>(sourceEntries.Length);

        foreach (var entry in sourceEntries)
        {
            if (IsContactNameEntry(entry))
            {
                if (!string.IsNullOrWhiteSpace(entry.Subtitle))
                {
                    normalized.Add(new CvExportEntry(
                        "Name",
                        entry.Subtitle.Trim(),
                        null,
                        string.Empty,
                        [],
                        string.Empty));
                }

                foreach (var bullet in ValuedContactBullets(entry))
                {
                    normalized.Add(CreateContactChannelEntry(bullet));
                }

                continue;
            }

            var valuedBullets = ValuedContactBullets(entry);

            if (valuedBullets.Count > 1)
            {
                foreach (var bullet in valuedBullets)
                {
                    normalized.Add(CreateContactChannelEntry(bullet));
                }

                continue;
            }

            normalized.Add(entry);
        }

        return normalized;
    }

    private static bool TryExpandImportLegacyContact(
        IReadOnlyList<CvExportEntry> entries,
        out IReadOnlyList<CvExportEntry> expanded)
    {
        expanded = Array.Empty<CvExportEntry>();

        if (entries.Count != 1)
        {
            return false;
        }

        var only = entries[0];
        var valuedBullets = ValuedContactBullets(only);

        // Import legacy: one entry, title is person name (not "Name"), channels in bullets.
        if (IsContactNameEntry(only) || valuedBullets.Count < 2 || IsKnownContactChannelLabel(only.Title))
        {
            return false;
        }

        var result = new List<CvExportEntry>(valuedBullets.Count + 1);
        var nameText = ResolveLegacyContactName(only);

        if (!string.IsNullOrWhiteSpace(nameText))
        {
            result.Add(new CvExportEntry("Name", nameText, null, string.Empty, [], string.Empty));
        }

        foreach (var bullet in valuedBullets)
        {
            result.Add(CreateContactChannelEntry(bullet));
        }

        expanded = result;
        return true;
    }

    private static string? ResolveLegacyContactName(CvExportEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Subtitle))
        {
            return entry.Subtitle.Trim();
        }

        var title = entry.Title?.Trim() ?? string.Empty;

        if (title.Length == 0 || IsChannelShapedContactLine(title))
        {
            return null;
        }

        return title;
    }

    private static bool IsKnownContactChannelLabel(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        return title.Trim().ToLowerInvariant() switch
        {
            "email" or "e-mail" or "phone" or "mobile" or "tel" or "telephone"
                or "linkedin" or "location" or "address" or "website" or "web" or "url" => true,
            _ => false
        };
    }

    private static bool IsChannelShapedContactLine(string value)
    {
        if (IsKnownContactChannelLabel(value))
        {
            return true;
        }

        // Prefer not treating emails / bare URLs as a person name when expanding legacy Contact.
        return value.Contains('@', StringComparison.Ordinal)
            || value.Contains("://", StringComparison.Ordinal)
            || value.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            || value.Contains("linkedin.com/", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ValuedContactBullets(CvExportEntry entry) =>
        entry.Bullets
            .Where((line) => !string.IsNullOrWhiteSpace(line))
            .Select((line) => line.Trim())
            .ToArray();

    private static CvExportEntry CreateContactChannelEntry(string value) =>
        new(string.Empty, null, null, string.Empty, [value], string.Empty);

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
            return !string.IsNullOrWhiteSpace(entry.Subtitle)
                || entry.Bullets.Any((bullet) => !string.IsNullOrWhiteSpace(bullet));
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
