using ApplyVault.Api.Services;

namespace ApplyVault.Api.Services.HtmlExport;

/// <summary>
/// Builds CompactLevel override CSS for HTML CV export (preview + PDF).
/// Shrink priority is encoded in the ramp: bullets/entries/sections/pads/fonts before photo floors.
/// Photo floors stay at or above 96px (preview-parity baselines: Modern 136 / Minimal 168).
/// CompactLevel 0 leaves template baselines untouched (no override CSS).
/// Levels 1–4 shrink denser than the Modern/Minimal baselines so a one-page export can fit
/// typical multi-section CVs.
/// </summary>
internal static class CvExportCompactCssBuilder
{
    public static string Build(CvPdfRenderOptions? renderOptions)
    {
        var compactLevel = Math.Clamp(renderOptions?.CompactLevel ?? 0, 0, CvPdfRenderOptions.MaxCompactLevel);

        if (compactLevel == 0)
        {
            return string.Empty;
        }

        var level = ResolveLevel(compactLevel);

        return $$"""

:root {
  --cv-page-pad-y: {{level.PadY}}mm !important;
  --cv-page-pad-x: {{level.PadX}}mm !important;
  --cv-page-pad-right: {{level.MinimalPadRight}}mm !important;
  --cv-space: {{level.SpacePt}}pt !important;
  --cv-space-block: {{level.SpaceBlockPt}}pt !important;
  --cv-space-section: {{level.SpaceSectionPt}}pt !important;
}

html body {
  font-size: {{FormatPercent(level.FontScale)}} !important;
  line-height: {{level.LineHeight:0.##}} !important;
}

.section {
  margin-bottom: {{level.SectionMarginPx}}px !important;
}

.section-title {
  margin-bottom: {{level.SectionTitleMarginPx}}px !important;
  padding-bottom: 2px !important;
}

.entry {
  margin-bottom: {{level.EntryMarginPx}}px !important;
}

.entry-summary,
.entry-bullets {
  margin-top: {{Math.Max(1, level.EntryMarginPx / 2)}}px !important;
}

.entry-bullets li {
  margin-bottom: {{level.BulletMarginPx}}px !important;
}

.entry-tech {
  margin-top: 2px !important;
}

.cv-body {
  padding: {{level.MinimalPadY}}mm {{level.MinimalPadRight}}mm {{level.MinimalPadY}}mm {{level.MinimalPadX}}mm !important;
}

.cv-layout .cv-sidebar {
  padding: {{level.PadY}}mm {{level.InnerGutterMm}}mm {{level.PadY}}mm {{level.PadX}}mm !important;
}

.cv-layout .cv-main {
  padding: {{level.PadY}}mm {{level.PadX}}mm {{level.PadY}}mm {{level.MainInnerGutterMm}}mm !important;
}

.cv-header {
  gap: {{level.HeaderGapPt}}pt !important;
  margin-bottom: {{level.SpaceSectionPt}}pt !important;
}

.cv-name {
  font-size: {{level.NameFontPt}}pt !important;
  margin-bottom: {{level.SpacePt}}pt !important;
}

.cv-layout .cv-sidebar .cv-photo {
  width: {{level.ModernPhotoPx}}px !important;
  height: {{level.ModernPhotoPx}}px !important;
  max-width: 100% !important;
  max-height: {{level.ModernPhotoPx}}px !important;
}

.cv-body .cv-photo {
  width: {{level.MinimalPhotoPx}}px !important;
  height: {{level.MinimalPhotoPx}}px !important;
  max-width: 100% !important;
  max-height: {{level.MinimalPhotoPx}}px !important;
}
""";
    }

    private static CompactLevelValues ResolveLevel(int compactLevel) =>
        compactLevel switch
        {
            1 => new(
                FontScale: 0.92m,
                LineHeight: 1.25m,
                SectionMarginPx: 8,
                SectionTitleMarginPx: 4,
                EntryMarginPx: 6,
                BulletMarginPx: 2,
                PadY: 12,
                PadX: 12,
                MinimalPadY: 12,
                MinimalPadX: 14,
                MinimalPadRight: 12,
                InnerGutterMm: 10,
                MainInnerGutterMm: 11,
                HeaderGapPt: 10,
                SpacePt: 2,
                SpaceBlockPt: 4,
                SpaceSectionPt: 8,
                NameFontPt: 18,
                ModernPhotoPx: 120,
                MinimalPhotoPx: 148),
            2 => new(
                FontScale: 0.86m,
                LineHeight: 1.18m,
                SectionMarginPx: 6,
                SectionTitleMarginPx: 3,
                EntryMarginPx: 5,
                BulletMarginPx: 1,
                PadY: 10,
                PadX: 11,
                MinimalPadY: 10,
                MinimalPadX: 12,
                MinimalPadRight: 11,
                InnerGutterMm: 9,
                MainInnerGutterMm: 10,
                HeaderGapPt: 9,
                SpacePt: 1,
                SpaceBlockPt: 3,
                SpaceSectionPt: 6,
                NameFontPt: 17,
                ModernPhotoPx: 108,
                MinimalPhotoPx: 132),
            3 => new(
                FontScale: 0.80m,
                LineHeight: 1.12m,
                SectionMarginPx: 5,
                SectionTitleMarginPx: 3,
                EntryMarginPx: 4,
                BulletMarginPx: 1,
                PadY: 9,
                PadX: 10,
                MinimalPadY: 9,
                MinimalPadX: 11,
                MinimalPadRight: 10,
                InnerGutterMm: 8,
                MainInnerGutterMm: 9,
                HeaderGapPt: 8,
                SpacePt: 1,
                SpaceBlockPt: 3,
                SpaceSectionPt: 5,
                NameFontPt: 16,
                ModernPhotoPx: 100,
                MinimalPhotoPx: 116),
            4 => new(
                FontScale: 0.74m,
                LineHeight: 1.05m,
                SectionMarginPx: 4,
                SectionTitleMarginPx: 2,
                EntryMarginPx: 3,
                BulletMarginPx: 0,
                PadY: 8,
                PadX: 9,
                MinimalPadY: 8,
                MinimalPadX: 10,
                MinimalPadRight: 10,
                InnerGutterMm: 7,
                MainInnerGutterMm: 8,
                HeaderGapPt: 7,
                SpacePt: 1,
                SpaceBlockPt: 2,
                SpaceSectionPt: 4,
                NameFontPt: 15,
                // Hard floor: do not shrink photos below 96px (preview-parity AC).
                ModernPhotoPx: 96,
                MinimalPhotoPx: 104),
            _ => new(
                FontScale: 1m,
                LineHeight: 1.4m,
                SectionMarginPx: 16,
                SectionTitleMarginPx: 10,
                EntryMarginPx: 12,
                BulletMarginPx: 4,
                PadY: 16,
                PadX: 18,
                MinimalPadY: 18,
                MinimalPadX: 20,
                MinimalPadRight: 16,
                InnerGutterMm: 12,
                MainInnerGutterMm: 14,
                HeaderGapPt: 12,
                SpacePt: 2,
                SpaceBlockPt: 6,
                SpaceSectionPt: 10,
                NameFontPt: 20,
                ModernPhotoPx: 136,
                MinimalPhotoPx: 168)
        };

    private static string FormatPercent(decimal value) => $"{value * 100m:0.#}%";

    private sealed record CompactLevelValues(
        decimal FontScale,
        decimal LineHeight,
        int SectionMarginPx,
        int SectionTitleMarginPx,
        int EntryMarginPx,
        int BulletMarginPx,
        int PadY,
        int PadX,
        int MinimalPadY,
        int MinimalPadX,
        int MinimalPadRight,
        int InnerGutterMm,
        int MainInnerGutterMm,
        int HeaderGapPt,
        int SpacePt,
        int SpaceBlockPt,
        int SpaceSectionPt,
        int NameFontPt,
        int ModernPhotoPx,
        int MinimalPhotoPx);
}
