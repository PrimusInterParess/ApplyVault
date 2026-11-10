using System.Globalization;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Services.HtmlExport;

/// <summary>
/// Builds CompactLevel override CSS for HTML CV export (preview + PDF).
/// Shrink priority is encoded in the ramp: bullets/entries/sections/pads/fonts before the photo floor.
/// The photo floor stays at or above 96px (template baseline 168px).
/// CompactLevel 0 leaves the template baseline untouched (no override CSS).
/// Levels 1–4 shrink progressively so an export lands on one page (recommended) and never
/// more than two pages (maximum) for typical multi-section CVs.
/// Type scale multiplies the template's own point sizes. The job title stays at least as large as the body.
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
        var scale = level.FontScale.ToString("0.##", CultureInfo.InvariantCulture);
        var lineHeight = level.LineHeight.ToString("0.##", CultureInfo.InvariantCulture);

        return $$"""

:root {
  --cv-font-scale: {{scale}};
  --cv-page-pad-y: {{level.PadY}}mm !important;
  --cv-page-pad-x: {{level.PadX}}mm !important;
  --cv-page-pad-right: {{level.PadRight}}mm !important;
  --cv-space: {{level.SpacePt}}pt !important;
  --cv-space-block: {{level.SpaceBlockPt}}pt !important;
  --cv-space-section: {{level.SpaceSectionPt}}pt !important;
}

html {
  font-size: 10.5pt;
}

html body {
  font-size: calc(10.5pt * var(--cv-font-scale)) !important;
  line-height: {{lineHeight}} !important;
}

.section {
  margin-bottom: {{level.SectionMarginPx}}px !important;
}

.section-title {
  margin-bottom: {{level.SectionTitleMarginPx}}px !important;
  padding-bottom: 2px !important;
  font-size: calc(8.5pt * var(--cv-font-scale)) !important;
}

.entry-title {
  font-size: calc(10.5pt * var(--cv-font-scale)) !important;
}

.entry-date,
.cv-contact-line {
  font-size: calc(9.5pt * var(--cv-font-scale)) !important;
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
  padding: {{level.PadY}}mm {{level.PadRight}}mm {{level.PadY}}mm {{level.PadX}}mm !important;
}

.cv-header {
  gap: {{level.HeaderGapPt}}pt !important;
  margin-bottom: {{level.SpaceSectionPt}}pt !important;
}

.cv-name {
  font-size: {{level.NameFontPt}}pt !important;
  margin-bottom: {{level.SpacePt}}pt !important;
}

.cv-body .cv-photo {
  width: {{level.PhotoPx}}px !important;
  height: {{level.PhotoPx}}px !important;
  max-width: 100% !important;
  max-height: {{level.PhotoPx}}px !important;
}
""";
    }

    private static CompactLevelValues ResolveLevel(int compactLevel) =>
        compactLevel switch
        {
            1 => new(
                FontScale: 0.96m,
                LineHeight: 1.25m,
                SectionMarginPx: 8,
                SectionTitleMarginPx: 4,
                EntryMarginPx: 6,
                BulletMarginPx: 2,
                PadY: 12,
                PadX: 14,
                PadRight: 14,
                HeaderGapPt: 10,
                SpacePt: 2,
                SpaceBlockPt: 4,
                SpaceSectionPt: 8,
                NameFontPt: 18,
                PhotoPx: 148),
            2 => new(
                FontScale: 0.92m,
                LineHeight: 1.18m,
                SectionMarginPx: 6,
                SectionTitleMarginPx: 3,
                EntryMarginPx: 5,
                BulletMarginPx: 1,
                PadY: 10,
                PadX: 12,
                PadRight: 12,
                HeaderGapPt: 9,
                SpacePt: 1,
                SpaceBlockPt: 3,
                SpaceSectionPt: 6,
                NameFontPt: 17,
                PhotoPx: 132),
            3 => new(
                FontScale: 0.86m,
                LineHeight: 1.12m,
                SectionMarginPx: 5,
                SectionTitleMarginPx: 3,
                EntryMarginPx: 4,
                BulletMarginPx: 1,
                PadY: 9,
                PadX: 11,
                PadRight: 11,
                HeaderGapPt: 8,
                SpacePt: 1,
                SpaceBlockPt: 3,
                SpaceSectionPt: 5,
                NameFontPt: 16,
                PhotoPx: 116),
            4 => new(
                FontScale: 0.80m,
                LineHeight: 1.05m,
                SectionMarginPx: 4,
                SectionTitleMarginPx: 2,
                EntryMarginPx: 3,
                BulletMarginPx: 0,
                PadY: 8,
                PadX: 10,
                PadRight: 10,
                HeaderGapPt: 7,
                SpacePt: 1,
                SpaceBlockPt: 2,
                SpaceSectionPt: 4,
                NameFontPt: 15,
                // Hard floor: do not shrink the photo below 96px (preview-parity AC).
                PhotoPx: 104),
            _ => new(
                FontScale: 1m,
                LineHeight: 1.4m,
                SectionMarginPx: 16,
                SectionTitleMarginPx: 10,
                EntryMarginPx: 12,
                BulletMarginPx: 4,
                PadY: 18,
                PadX: 20,
                PadRight: 20,
                HeaderGapPt: 12,
                SpacePt: 2,
                SpaceBlockPt: 6,
                SpaceSectionPt: 10,
                NameFontPt: 20,
                PhotoPx: 168)
        };

    private sealed record CompactLevelValues(
        decimal FontScale,
        decimal LineHeight,
        int SectionMarginPx,
        int SectionTitleMarginPx,
        int EntryMarginPx,
        int BulletMarginPx,
        int PadY,
        int PadX,
        int PadRight,
        int HeaderGapPt,
        int SpacePt,
        int SpaceBlockPt,
        int SpaceSectionPt,
        int NameFontPt,
        int PhotoPx);
}
