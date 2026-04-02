import type { ScrapeResult } from '../shared/models/scrapeResult';
import { evaluateScrapeResult, getScrapeResultScore } from '../shared/utils/scrapeQuality';
import { extractVisibleText } from './extractVisibleText';

const EXTRACTION_DELAYS_MS = [0, 400, 1200, 2500];

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    window.setTimeout(resolve, ms);
  });
}

function withExtractionMetadata(result: ScrapeResult, attempts: number): ScrapeResult {
  return {
    ...result,
    extraction: {
      ...evaluateScrapeResult(result),
      attempts
    }
  };
}

export async function extractVisibleTextWithRetries(documentRef: Document = document): Promise<ScrapeResult> {
  let bestResult: ScrapeResult | null = null;
  let lastError: Error | null = null;

  for (let index = 0; index < EXTRACTION_DELAYS_MS.length; index += 1) {
    const delayMs = EXTRACTION_DELAYS_MS[index];

    if (delayMs > 0) {
      await delay(delayMs);
    }

    try {
      const attemptNumber = index + 1;
      const result = withExtractionMetadata(extractVisibleText(documentRef), attemptNumber);

      if (!bestResult || getScrapeResultScore(result) > getScrapeResultScore(bestResult)) {
        bestResult = result;
      }

      if (result.extraction?.status === 'valid') {
        return result;
      }
    } catch (error) {
      lastError = error instanceof Error ? error : new Error('Text extraction failed.');
    }
  }

  if (bestResult) {
    return withExtractionMetadata(bestResult, EXTRACTION_DELAYS_MS.length);
  }

  throw lastError ?? new Error('Text extraction failed.');
}
