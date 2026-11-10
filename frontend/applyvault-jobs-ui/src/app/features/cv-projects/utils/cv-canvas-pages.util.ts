/** CSS px for A4 at 96dpi: 210mm × 297mm. */
export const CV_A4_WIDTH_PX = 794;
export const CV_A4_HEIGHT_PX = 1123;

/** Desk gap between stacked edit-canvas sheets. Wide enough to read as two pages. */
export const CV_PAGE_GAP_PX = 72;

/**
 * Type scales for the edit canvas, matching the export compact ramp and one
 * tighter step for the editing chrome. The first scale that fits on one sheet is used.
 */
export const CV_CANVAS_FIT_SCALES = [1, 0.96, 0.92, 0.86, 0.8, 0.74] as const;

export interface CvCanvasPageSheet {
  readonly page: number;
  readonly top: number;
}

/**
 * Extra margin that moves a block onto the next edit-canvas sheet.
 * A block that fits on one page moves when it does not fit in the room left,
 * even when that leaves a large empty tail.
 * A block taller than one page stays put so its children can continue.
 * pagePadTopPx repeats the page-1 top inset on the sheet the block moves to.
 * The canvas uses a tighter type scale first so a CV that fits on one page stays on one sheet.
 */
export function cvPageBreakMargin(
  offsetTop: number,
  blockHeight: number,
  pageHeightPx = CV_A4_HEIGHT_PX,
  gapPx = CV_PAGE_GAP_PX,
  pagePadTopPx = 0
): number {
  if (blockHeight <= 0 || pageHeightPx <= 0 || blockHeight > pageHeightPx) {
    return 0;
  }

  const stride = pageHeightPx + gapPx;
  const pageIndex = Math.max(0, Math.floor(Math.max(0, offsetTop) / stride));
  const pageStart = pageIndex * stride;
  const contentEnd = pageStart + pageHeightPx;

  if (offsetTop + blockHeight <= contentEnd + 0.5 && offsetTop < contentEnd) {
    return 0;
  }

  const inset = Math.max(0, pagePadTopPx);
  return Math.max(0, pageStart + stride + inset - offsetTop);
}

/** True when the block's box runs past the sheet it starts on. */
export function cvBlockOverflowsPage(
  offsetTop: number,
  blockHeight: number,
  pageHeightPx = CV_A4_HEIGHT_PX,
  gapPx = CV_PAGE_GAP_PX
): boolean {
  if (blockHeight <= 0 || pageHeightPx <= 0) {
    return false;
  }

  const stride = pageHeightPx + gapPx;
  const pageIndex = Math.max(0, Math.floor(Math.max(0, offsetTop) / stride));
  const contentEnd = pageIndex * stride + pageHeightPx;

  return offsetTop < contentEnd && offsetTop + blockHeight > contentEnd + 0.5;
}

/** One sheet per started page, including a short first page. */
export function cvCanvasPageSheets(
  contentHeightPx: number,
  pageHeightPx = CV_A4_HEIGHT_PX,
  gapPx = CV_PAGE_GAP_PX
): readonly CvCanvasPageSheet[] {
  const sheets: CvCanvasPageSheet[] = [{ page: 1, top: 0 }];

  if (pageHeightPx <= 0) {
    return sheets;
  }

  let nextTop = pageHeightPx + gapPx;

  while (sheets.length < 20) {
    const previousContentEnd = nextTop - gapPx;

    if (contentHeightPx <= previousContentEnd + 0.5) {
      break;
    }

    sheets.push({ page: sheets.length + 1, top: nextTop });
    nextTop += pageHeightPx + gapPx;
  }

  return sheets;
}

export function cvCanvasStackHeight(
  contentHeightPx: number,
  pageHeightPx = CV_A4_HEIGHT_PX,
  gapPx = CV_PAGE_GAP_PX
): number {
  const sheets = cvCanvasPageSheets(contentHeightPx, pageHeightPx, gapPx);
  return sheets[sheets.length - 1].top + pageHeightPx;
}
