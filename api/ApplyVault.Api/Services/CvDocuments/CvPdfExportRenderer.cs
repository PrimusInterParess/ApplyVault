using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ApplyVault.Api.Services;

public sealed class CvPdfExportRenderer : ICvPdfExportRenderer
{
    private const string AccentColor = "#1B4F72";
    private const string MutedColor = "#5D6D7E";
    private const string RuleColor = "#D5DBDB";
    private const string SectionBackground = "#F7F9FB";

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

        var headerSection = orderedSections.FirstOrDefault((section) =>
            section.SectionType == CvSectionTypes.Summary)
            ?? orderedSections[0];
        var bodySections = orderedSections
            .Where((section) => section != headerSection)
            .ToArray();

        var document = Document.Create((container) =>
        {
            container.Page((page) =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(44);
                page.MarginVertical(40);
                page.DefaultTextStyle((style) => style.FontSize(10).FontColor(Colors.Black).LineHeight(1.35f));

                page.Content().Column((column) =>
                {
                    column.Spacing(14);

                    column.Item().Element((item) => ComposeHeader(
                        item,
                        headerSection,
                        request.ProfilePhotoBytes));

                    foreach (var section in bodySections)
                    {
                        column.Item().Element((item) => ComposeSection(item, section));
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, CvExportSection section, byte[]? photoBytes)
    {
        container.Column((column) =>
        {
            column.Item().Row((row) =>
            {
                row.RelativeItem().Column((textColumn) =>
                {
                    textColumn.Spacing(6);

                    foreach (var entry in section.Entries.Where(EntryHasContent))
                    {
                        textColumn.Item().Element((item) => ComposeEntry(
                            item,
                            entry,
                            section.SectionType,
                            compact: true,
                            showSeparator: false));
                    }
                });

                if (photoBytes is { Length: > 0 })
                {
                    row.ConstantItem(96).Height(96).Element((photoContainer) =>
                    {
                        photoContainer
                            .Border(1)
                            .BorderColor(RuleColor)
                            .Background(Colors.White)
                            .Padding(4)
                            .Image(photoBytes)
                            .FitArea();
                    });
                }
            });

            column.Item().PaddingTop(6).LineHorizontal(2).LineColor(AccentColor);
        });
    }

    private static void ComposeSection(IContainer container, CvExportSection section)
    {
        container.Column((column) =>
        {
            column.Spacing(10);

            column.Item().Background(SectionBackground).Padding(10).Column((header) =>
            {
                header.Item().Text(section.Heading.ToUpperInvariant())
                    .FontSize(10)
                    .Bold()
                    .FontColor(AccentColor)
                    .LetterSpacing(0.6f);

                header.Item().PaddingTop(4).LineHorizontal(1).LineColor(AccentColor);
            });

            column.Item().PaddingHorizontal(4).Column((body) =>
            {
                body.Spacing(12);

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
            column.Spacing(compact ? 4 : 6);

            if (!string.IsNullOrWhiteSpace(entry.Title))
            {
                column.Item().Row((row) =>
                {
                    row.RelativeItem().Text(entry.Title).FontSize(compact ? 12 : 11).Bold();

                    if (!string.IsNullOrWhiteSpace(entry.DateRange))
                    {
                        row.ConstantItem(130).AlignRight().Text(entry.DateRange)
                            .FontSize(9)
                            .FontColor(MutedColor);
                    }
                });
            }
            else if (!string.IsNullOrWhiteSpace(entry.DateRange))
            {
                column.Item().AlignRight().Text(entry.DateRange)
                    .FontSize(9)
                    .FontColor(MutedColor);
            }

            if (!string.IsNullOrWhiteSpace(entry.Subtitle))
            {
                column.Item().Text(entry.Subtitle).FontSize(9.5f).FontColor(MutedColor).Italic();
            }

            foreach (var paragraph in CvExportTextNormalizer.Paragraphs(entry.Summary))
            {
                column.Item().Text(paragraph).FontSize(compact ? 10.5f : 10);
            }

            var bullets = GetDisplayBullets(entry, sectionType);

            if (bullets.Count > 0)
            {
                column.Item().PaddingLeft(8).Column((bulletColumn) =>
                {
                    bulletColumn.Spacing(3);

                    foreach (var bullet in bullets)
                    {
                        bulletColumn.Item().Row((row) =>
                        {
                            row.ConstantItem(12).Text("•").FontSize(10);
                            row.RelativeItem().Text(bullet).FontSize(10);
                        });
                    }
                });
            }

            var techItems = CvExportTextNormalizer.TechItems(entry.TechStack);

            if (techItems.Count > 0 && sectionType != CvSectionTypes.Skills)
            {
                column.Item().Text(text =>
                {
                    text.Span("Technologies: ").FontSize(9).SemiBold().FontColor(MutedColor);
                    text.Span(string.Join(", ", techItems)).FontSize(9).FontColor(MutedColor);
                });
            }

            if (showSeparator)
            {
                column.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(RuleColor);
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
