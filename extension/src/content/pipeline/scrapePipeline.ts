import type { ScrapeResult } from '../../shared/models/scrapeResult';
import { extractJobDetails } from '../jobDetailsExtraction/extractJobDetails';
import { extractPageText } from './visibleTextExtractor';

export function runScrapePipeline(documentRef: Document = document): ScrapeResult {
  const pageText = extractPageText(documentRef);

  return {
    title: pageText.title,
    url: pageText.url,
    text: pageText.text,
    textLength: pageText.textLength,
    extractedAt: new Date().toISOString(),
    jobDetails: extractJobDetails(documentRef, pageText.text)
  };
}
