using ApplyVault.Api.Services;

namespace ApplyVault.Api.Services.HtmlExport;

/// <summary>
/// Builds CompactLevel override CSS for HTML CV export (preview + PDF).
/// Shrink priority is encoded in the ramp: bullets/entries/sections/pads/fonts before photo floors.
/// Photo floors stay at or above 96px (preview-parity baselines: Classic 140 / Modern 136 / Minimal 128).
/// CompactLevel 0 leaves template baselines untouched (no override CSS).
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
  margin-top: {{Math.Max(2, level.EntryMarginPx / 2)}}px !important;
}

.entry-bullets li {
  margin-bottom: {{level.BulletMarginPx}}px !important;
}

.entry-tech {
  margin-top: 2px !important;
}

.cv-classic {
  padding: var(--cv-page-pad-y) var(--cv-page-pad-x) !important;
}

.cv-body {
  padding: {{level.MinimalPadY}}mm {{level.MinimalPadX}}mm !important;
}

.cv-layout .cv-sidebar {
  padding: {{level.PadY}}mm {{level.InnerGutterMm}}mm {{level.PadY}}mm {{level.PadX}}mm !important;
}

.cv-layout .cv-main {
  padding: {{level.PadY}}mm {{level.PadX}}mm {{level.PadY}}mm {{level.MainInnerGutterMm}}mm !important;
}

.cv-header {
  gap: {{level.HeaderGapPt}}pt !important;
}

.cv-classic .cv-photo {
  width: {{level.ClassicPhotoPx}}px !important;
  height: {{level.ClassicPhotoPx}}px !important;
  max-width: 100% !important;
  max-height: {{level.ClassicPhotoPx}}px !important;
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
                FontScale: 0.94m,
                LineHeight: 1.28m,
                SectionMarginPx: 16,
                SectionTitleMarginPx: 8,
                EntryMarginPx: 12,
                BulletMarginPx: 3,
                PadY: 14,
                PadX: 16,
                MinimalPadY: 16,
                MinimalPadX: 18,
                InnerGutterMm: 11,
                MainInnerGutterMm: 12,
                HeaderGapPt: 14,
                ClassicPhotoPx: 128,
                ModernPhotoPx: 124,
                MinimalPhotoPx: 120),
            2 => new(
                FontScale: 0.88m,
                LineHeight: 1.2m,
                SectionMarginPx: 13,
                SectionTitleMarginPx: 7,
                EntryMarginPx: 10,
                BulletMarginPx: 2,
                PadY: 12,
                PadX: 14,
                MinimalPadY: 14,
                MinimalPadX: 16,
                InnerGutterMm: 10,
                MainInnerGutterMm: 11,
                HeaderGapPt: 12,
                ClassicPhotoPx: 116,
                ModernPhotoPx: 112,
                MinimalPhotoPx: 108),
            3 => new(
                FontScale: 0.82m,
                LineHeight: 1.12m,
                SectionMarginPx: 12,
                SectionTitleMarginPx: 6,
                EntryMarginPx: 9,
                BulletMarginPx: 1,
                PadY: 11,
                PadX: 13,
                MinimalPadY: 12,
                MinimalPadX: 14,
                InnerGutterMm: 9,
                MainInnerGutterMm: 10,
                HeaderGapPt: 11,
                ClassicPhotoPx: 104,
                ModernPhotoPx: 100,
                MinimalPhotoPx: 96),
            4 => new(
                FontScale: 0.76m,
                LineHeight: 1.05m,
                SectionMarginPx: 11,
                SectionTitleMarginPx: 6,
                EntryMarginPx: 8,
                BulletMarginPx: 1,
                PadY: 10,
                PadX: 12,
                MinimalPadY: 10,
                MinimalPadX: 12,
                InnerGutterMm: 8,
                MainInnerGutterMm: 8,
                HeaderGapPt: 10,
                // Hard floor: do not shrink photos below 96px (preview-parity AC).
                ClassicPhotoPx: 96,
                ModernPhotoPx: 96,
                MinimalPhotoPx: 96),
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
                InnerGutterMm: 12,
                MainInnerGutterMm: 14,
                HeaderGapPt: 16,
                ClassicPhotoPx: 140,
                ModernPhotoPx: 136,
                MinimalPhotoPx: 128)
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
        int InnerGutterMm,
        int MainInnerGutterMm,
        int HeaderGapPt,
        int ClassicPhotoPx,
        int ModernPhotoPx,
        int MinimalPhotoPx);
}
