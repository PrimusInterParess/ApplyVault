import type { ExtractionMetadata, ScrapeResult } from '../../shared/models/scrapeResult';
import { evaluateScrapeResult, getScrapeResultScore } from '../../shared/utils/scrapeQuality';
import { waitForExtractionSignals } from './domWait';
import { runScrapePipeline } from './scrapePipeline';

const EXTRACTION_DELAYS_MS = [0, 400, 1200, 2500];

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    window.setTimeout(resolve, ms);
  });
}

function withAttemptsOnly(result: ScrapeResult, attempts: number): ScrapeResult {
  const placeholder: ExtractionMetadata = {
    status: 'partial',
    issues: [],
    attempts
  };

  return {
    ...result,
    extraction: placeholder
  };
}

export async function extractVisibleTextWithRetries(documentRef: Document = document): Promise<ScrapeResult> {
  await waitForExtractionSignals(documentRef);

  let bestResult: ScrapeResult | null = null;
  let lastError: Error | null = null;

  for (let index = 0; index < EXTRACTION_DELAYS_MS.length; index += 1) {
    const delayMs = EXTRACTION_DELAYS_MS[index];

    if (delayMs > 0) {
      await delay(delayMs);
    }

    try {
      const attemptNumber = index + 1;
      const result = runScrapePipeline(documentRef);

      if (!bestResult || getScrapeResultScore(result) > getScrapeResultScore(bestResult)) {
        bestResult = withAttemptsOnly(result, attemptNumber);
      }

      if (evaluateScrapeResult(result).status === 'valid') {
        return withAttemptsOnly(result, attemptNumber);
      }
    } catch (error) {
      lastError = error instanceof Error ? error : new Error('Text extraction failed.');
    }
  }

  if (bestResult) {
    return withAttemptsOnly(bestResult, EXTRACTION_DELAYS_MS.length);
  }

  throw lastError ?? new Error('Text extraction failed.');
}
