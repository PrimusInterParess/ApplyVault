using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ApplyVault.Api.Services;

public sealed class CvPdfExportRenderer : ICvPdfExportRenderer
{
    private const string AccentColor = "#1B4F72";
    private const string MutedColor = "#5D6D7E";
    private const string RuleColor = "#D5DBDB";

    public byte[] Render(CvExportRenderRequest request)
    {
        if (!HasRenderableContent(request.Sections))
        {
            throw new InvalidOperationException("No CV content is available to export.");
        }

        var orderedSections = request.Sections
            .Where(SectionHasContent)
            .OrderBy((section) => section.SortOrder)
            .ToArray();

        var headerSections = orderedSections
            .Where(IsHeaderSection)
            .ToArray();
        var bodySections = orderedSections
            .Where((section) => !IsHeaderSection(section))
            .ToArray();

        if (headerSections.Length == 0 && bodySections.Length > 0)
        {
            headerSections = [bodySections[0]];
            bodySections = bodySections.Skip(1).ToArray();
        }

        var document = Document.Create((container) =>
        {
            container.Page((page) =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(36);
                page.MarginVertical(32);
                page.DefaultTextStyle((style) => style.FontSize(9.5f).FontColor(Colors.Black).LineHeight(1.22f));

                page.Content().Column((column) =>
                {
                    column.Spacing(8);

                    if (headerSections.Length > 0)
                    {
                        column.Item().Element((item) => ComposeHeader(
                            item,
                            headerSections,
                            request.ProfilePhotoBytes));
                    }

                    foreach (var section in bodySections)
                    {
                        column.Item().Element((item) => ComposeSection(item, section));
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(
        IContainer container,
        IReadOnlyList<CvExportSection> headerSections,
        byte[]? photoBytes)
    {
        container.Column((column) =>
        {
            column.Item().Row((row) =>
            {
                row.RelativeItem().Column((textColumn) =>
                {
                    textColumn.Spacing(3);

                    foreach (var section in headerSections)
                    {
                        foreach (var entry in section.Entries.Where(EntryHasContent))
                        {
                            textColumn.Item().Element((item) => ComposeHeaderEntry(
                                item,
                                entry,
                                section));
                        }
                    }
                });

                if (photoBytes is { Length: > 0 })
                {
                    row.ConstantItem(80).Height(80).Element((photoContainer) =>
                    {
                        photoContainer
                            .Border(1)
                            .BorderColor(RuleColor)
                            .Background(Colors.White)
                            .Padding(2)
                            .Image(photoBytes)
                            .FitArea();
                    });
                }
            });

            column.Item().PaddingTop(4).LineHorizontal(1.25f).LineColor(AccentColor);
        });
    }

    private static void ComposeHeaderEntry(IContainer container, CvExportEntry entry, CvExportSection section)
    {
        if (IsContactSection(section))
        {
            ComposeContactHeaderEntry(container, entry);
            return;
        }

        ComposeEntry(container, entry, section.SectionType, compact: true, showSeparator: false);
    }

    private static void ComposeContactHeaderEntry(IContainer container, CvExportEntry entry)
    {
        container.Column((column) =>
        {
            column.Spacing(2);

            if (!string.IsNullOrWhiteSpace(entry.Title))
            {
                column.Item().Text(entry.Title).FontSize(13).Bold().FontColor(AccentColor);
            }

            var contactLines = entry.Bullets.Count > 0
                ? entry.Bullets
                : CvExportTextNormalizer.Paragraphs(entry.Summary);

            if (contactLines.Count > 0)
            {
                column.Item().Text(string.Join("  |  ", contactLines))
                    .FontSize(8.5f)
                    .FontColor(MutedColor);
            }
        });
    }

    private static void ComposeSection(IContainer container, CvExportSection section)
    {
        container.Column((column) =>
        {
            column.Spacing(4);

            column.Item().Column((header) =>
            {
                header.Item().Text(section.Heading.ToUpperInvariant())
                    .FontSize(8.5f)
                    .Bold()
                    .FontColor(AccentColor)
                    .LetterSpacing(0.35f);

                header.Item().PaddingTop(2).LineHorizontal(0.75f).LineColor(AccentColor);
            });

            column.Item().Column((body) =>
            {
                body.Spacing(5);

                var entries = section.Entries.Where(EntryHasContent).ToArray();

                for (var index = 0; index < entries.Length; index++)
                {
                    var entry = entries[index];
                    var showSeparator = index < entries.Length - 1;

                    body.Item().Element((item) => ComposeEntry(
                        item,
                        entry,
                        section.SectionType,
                        compact: false,
                        showSeparator));
                }
            });
        });
    }

    private static void ComposeEntry(
        IContainer container,
        CvExportEntry entry,
        string sectionType,
        bool compact,
        bool showSeparator)
    {
        container.Column((column) =>
        {
            column.Spacing(compact ? 2 : 3);

            if (!string.IsNullOrWhiteSpace(entry.Title))
            {
                column.Item().Row((row) =>
                {
                    row.RelativeItem().Text(entry.Title).FontSize(compact ? 10.5f : 10).Bold();

                    if (!string.IsNullOrWhiteSpace(entry.DateRange))
                    {
                        row.ConstantItem(120).AlignRight().Text(entry.DateRange)
                            .FontSize(8.5f)
                            .FontColor(MutedColor);
                    }
                });
            }
            else if (!string.IsNullOrWhiteSpace(entry.DateRange))
            {
                column.Item().AlignRight().Text(entry.DateRange)
                    .FontSize(8.5f)
                    .FontColor(MutedColor);
            }

            if (!string.IsNullOrWhiteSpace(entry.Subtitle))
            {
                column.Item().Text(entry.Subtitle).FontSize(9).FontColor(MutedColor).Italic();
            }

            foreach (var paragraph in CvExportTextNormalizer.Paragraphs(entry.Summary))
            {
                column.Item().Text(paragraph).FontSize(compact ? 9.5f : 9.5f);
            }

            var bullets = GetDisplayBullets(entry, sectionType);

            if (bullets.Count > 0)
            {
                column.Item().PaddingLeft(6).Column((bulletColumn) =>
                {
                    bulletColumn.Spacing(1.5f);

                    foreach (var bullet in bullets)
                    {
                        bulletColumn.Item().Row((row) =>
                        {
                            row.ConstantItem(10).Text("•").FontSize(9.5f);
                            row.RelativeItem().Text(bullet).FontSize(9.5f);
                        });
                    }
                });
            }

            var techItems = CvExportTextNormalizer.TechItems(entry.TechStack);

            if (techItems.Count > 0 && sectionType != CvSectionTypes.Skills)
            {
                column.Item().Text(text =>
                {
                    text.Span("Technologies: ").FontSize(8.5f).SemiBold().FontColor(MutedColor);
                    text.Span(string.Join(", ", techItems)).FontSize(8.5f).FontColor(MutedColor);
                });
            }

            if (showSeparator)
            {
                column.Item().PaddingTop(1).LineHorizontal(0.35f).LineColor(RuleColor);
            }
        });
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

    private static bool IsHeaderSection(CvExportSection section) =>
        section.SectionType == CvSectionTypes.Summary || IsContactSection(section);

    private static bool IsContactSection(CvExportSection section) =>
        section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase);

    private static bool HasRenderableContent(IReadOnlyList<CvExportSection> sections) =>
        sections.Any(SectionHasContent);

    private static bool SectionHasContent(CvExportSection section) =>
        section.Entries.Any(EntryHasContent) || !string.IsNullOrWhiteSpace(section.Heading);

    private static bool EntryHasContent(CvExportEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.Title)
        || !string.IsNullOrWhiteSpace(entry.Subtitle)
        || !string.IsNullOrWhiteSpace(entry.DateRange)
        || !string.IsNullOrWhiteSpace(entry.Summary)
        || entry.Bullets.Count > 0
        || !string.IsNullOrWhiteSpace(entry.TechStack);
}
