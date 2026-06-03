using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ApplyVault.Api.Services;

public sealed class CvPdfExportRenderer : ICvPdfExportRenderer
{
    private const string AccentColor = "#1B4F72";
    private const string MutedColor = "#5D6D7E";
    private const string RuleColor = "#D5DBDB";

    public byte[] Render(CvExportRenderRequest request, CvPdfRenderOptions? options = null)
    {
        if (!HasRenderableContent(request.Sections))
        {
            throw new InvalidOperationException("No CV content is available to export.");
        }

        var layout = CvPdfClassicLayout.From(options);

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
                page.MarginHorizontal(layout.HorizontalMargin);
                page.MarginVertical(layout.VerticalMargin);
                page.DefaultTextStyle((style) => style
                    .FontSize(layout.BodyFontSize)
                    .FontColor(Colors.Black)
                    .LineHeight(layout.LineHeight));

                page.Content().Column((column) =>
                {
                    column.Spacing(layout.SectionSpacing);

                    if (headerSections.Length > 0)
                    {
                        column.Item().Element((item) => ComposeHeader(
                            item,
                            headerSections,
                            request.ProfilePhotoBytes,
                            layout));
                    }

                    foreach (var section in bodySections)
                    {
                        column.Item().Element((item) => ComposeSection(item, section, layout));
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(
        IContainer container,
        IReadOnlyList<CvExportSection> headerSections,
        byte[]? photoBytes,
        CvPdfClassicLayout layout)
    {
        container.Column((column) =>
        {
            column.Item().Row((row) =>
            {
                row.RelativeItem().Column((textColumn) =>
                {
                    textColumn.Spacing(layout.HeaderTextSpacing);

                    foreach (var section in headerSections)
                    {
                        foreach (var entry in section.Entries.Where(EntryHasContent))
                        {
                            textColumn.Item().Element((item) => ComposeHeaderEntry(
                                item,
                                entry,
                                section,
                                layout));
                        }
                    }
                });

                if (photoBytes is { Length: > 0 })
                {
                    row.ConstantItem(layout.PhotoSize).Height(layout.PhotoSize).Element((photoContainer) =>
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

            column.Item().PaddingTop(layout.RulePaddingTop).LineHorizontal(1.25f).LineColor(AccentColor);
        });
    }

    private static void ComposeHeaderEntry(
        IContainer container,
        CvExportEntry entry,
        CvExportSection section,
        CvPdfClassicLayout layout)
    {
        if (IsContactSection(section))
        {
            ComposeContactHeaderEntry(container, entry, layout);
            return;
        }

        ComposeEntry(container, entry, section.SectionType, compact: true, showSeparator: false, layout);
    }

    private static void ComposeContactHeaderEntry(
        IContainer container,
        CvExportEntry entry,
        CvPdfClassicLayout layout)
    {
        container.Column((column) =>
        {
            column.Spacing(layout.ContactSpacing);

            if (!string.IsNullOrWhiteSpace(entry.Title))
            {
                column.Item().Text(text =>
                {
                    CvExportInlinePdfRenderer.AppendRuns(
                        text,
                        entry.Title,
                        (span) => span.FontSize(layout.NameFontSize).Bold().FontColor(AccentColor));
                });
            }

            var contactLines = entry.Bullets.Count > 0
                ? entry.Bullets
                : CvExportTextNormalizer.Paragraphs(entry.Summary);

            if (contactLines.Count > 0)
            {
                column.Item().Text(text =>
                {
                    for (var lineIndex = 0; lineIndex < contactLines.Count; lineIndex++)
                    {
                        if (lineIndex > 0)
                        {
                            text.Span("  |  ").FontSize(layout.SmallFontSize).FontColor(MutedColor);
                        }

                        CvExportInlinePdfRenderer.AppendRuns(
                            text,
                            contactLines[lineIndex],
                            (span) => span.FontSize(layout.SmallFontSize).FontColor(MutedColor));
                    }
                });
            }
        });
    }

    private static void ComposeSection(IContainer container, CvExportSection section, CvPdfClassicLayout layout)
    {
        container.Column((column) =>
        {
            column.Spacing(layout.SectionInnerSpacing);

            column.Item().Column((header) =>
            {
                header.Item().Text(section.Heading.ToUpperInvariant())
                    .FontSize(layout.SectionTitleFontSize)
                    .Bold()
                    .FontColor(AccentColor)
                    .LetterSpacing(0.35f);

                header.Item().PaddingTop(layout.SectionRulePaddingTop).LineHorizontal(0.75f).LineColor(AccentColor);
            });

            column.Item().Column((body) =>
            {
                body.Spacing(layout.EntrySpacing);

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
                        showSeparator,
                        layout));
                }
            });
        });
    }

    private static void ComposeEntry(
        IContainer container,
        CvExportEntry entry,
        string sectionType,
        bool compact,
        bool showSeparator,
        CvPdfClassicLayout layout)
    {
        container.Column((column) =>
        {
            column.Spacing(compact ? layout.CompactEntryInnerSpacing : layout.EntryInnerSpacing);

            if (!string.IsNullOrWhiteSpace(entry.Title))
            {
                column.Item().Row((row) =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        CvExportInlinePdfRenderer.AppendRuns(
                            text,
                            entry.Title,
                            (span) => span.FontSize(compact ? layout.HeaderEntryTitleFontSize : layout.EntryTitleFontSize).Bold());
                    });

                    if (!string.IsNullOrWhiteSpace(entry.DateRange))
                    {
                        row.ConstantItem(layout.DateColumnWidth).AlignRight().Text(entry.DateRange)
                            .FontSize(layout.SmallFontSize)
                            .FontColor(MutedColor);
                    }
                });
            }
            else if (!string.IsNullOrWhiteSpace(entry.DateRange))
            {
                column.Item().AlignRight().Text(entry.DateRange)
                    .FontSize(layout.SmallFontSize)
                    .FontColor(MutedColor);
            }

            if (!string.IsNullOrWhiteSpace(entry.Subtitle))
            {
                column.Item().Text(text =>
                {
                    CvExportInlinePdfRenderer.AppendRuns(
                        text,
                        entry.Subtitle,
                        (span) => span.FontSize(layout.SubtitleFontSize).FontColor(MutedColor).Italic());
                });
            }

            foreach (var paragraph in CvExportTextNormalizer.Paragraphs(entry.Summary))
            {
                column.Item().Text(text =>
                {
                    CvExportInlinePdfRenderer.AppendRuns(
                        text,
                        paragraph,
                        (span) => span.FontSize(layout.BodyFontSize));
                });
            }

            var bullets = GetDisplayBullets(entry, sectionType);

            if (bullets.Count > 0)
            {
                column.Item().PaddingLeft(layout.BulletPaddingLeft).Column((bulletColumn) =>
                {
                    bulletColumn.Spacing(layout.BulletSpacing);

                    foreach (var bullet in bullets)
                    {
                        bulletColumn.Item().Row((row) =>
                        {
                            row.ConstantItem(layout.BulletMarkerWidth).Text("•").FontSize(layout.BodyFontSize);
                            row.RelativeItem().Text(text =>
                            {
                                CvExportInlinePdfRenderer.AppendRuns(
                                    text,
                                    bullet,
                                    (span) => span.FontSize(layout.BodyFontSize));
                            });
                        });
                    }
                });
            }

            var techItems = CvExportTextNormalizer.TechItems(entry.TechStack);

            if (techItems.Count > 0 && sectionType != CvSectionTypes.Skills)
            {
                column.Item().Text(text =>
                {
                    text.Span("Technologies: ").FontSize(layout.SmallFontSize).SemiBold().FontColor(MutedColor);
                    text.Span(string.Join(", ", techItems)).FontSize(layout.SmallFontSize).FontColor(MutedColor);
                });
            }

            if (showSeparator)
            {
                column.Item().PaddingTop(layout.SeparatorPaddingTop).LineHorizontal(0.35f).LineColor(RuleColor);
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

    private sealed record CvPdfClassicLayout(
        float Scale,
        float HorizontalMargin,
        float VerticalMargin,
        float LineHeight,
        float PhotoSize)
    {
        public float BodyFontSize => 9.5f * Scale;
        public float SmallFontSize => 8.5f * Scale;
        public float SubtitleFontSize => 9f * Scale;
        public float EntryTitleFontSize => 10f * Scale;
        public float HeaderEntryTitleFontSize => 10.5f * Scale;
        public float NameFontSize => 13f * Scale;
        public float SectionTitleFontSize => 8.5f * Scale;
        public float SectionSpacing => 8f * Scale;
        public float SectionInnerSpacing => 4f * Scale;
        public float SectionRulePaddingTop => 2f * Scale;
        public float EntrySpacing => 5f * Scale;
        public float EntryInnerSpacing => 3f * Scale;
        public float CompactEntryInnerSpacing => 2f * Scale;
        public float HeaderTextSpacing => 3f * Scale;
        public float ContactSpacing => 2f * Scale;
        public float BulletSpacing => 1.5f * Scale;
        public float BulletPaddingLeft => 6f * Scale;
        public float BulletMarkerWidth => 10f * Scale;
        public float DateColumnWidth => 120f * Scale;
        public float RulePaddingTop => 4f * Scale;
        public float SeparatorPaddingTop => 1f * Scale;

        public static CvPdfClassicLayout From(CvPdfRenderOptions? options)
        {
            var compactLevel = Math.Clamp(options?.CompactLevel ?? 0, 0, CvPdfRenderOptions.MaxCompactLevel);

            return compactLevel switch
            {
                1 => new CvPdfClassicLayout(0.94f, 32f, 26f, 1.16f, 72f),
                2 => new CvPdfClassicLayout(0.88f, 28f, 22f, 1.1f, 64f),
                3 => new CvPdfClassicLayout(0.82f, 24f, 18f, 1.04f, 56f),
                4 => new CvPdfClassicLayout(0.76f, 20f, 14f, 1.0f, 48f),
                _ => new CvPdfClassicLayout(1f, 36f, 32f, 1.22f, 80f)
            };
        }
    }
}
