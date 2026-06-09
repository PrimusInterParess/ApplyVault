import type { ExtractionIssue, JobDetails, ScrapeResult } from '../shared/models/scrapeResult';
import type { EditableControl } from './popupTypes';

function formatPageType(pageType: JobDetails['detectedPageType']): string {
  return pageType
    .split('-')
    .map((part) => `${part.charAt(0).toUpperCase()}${part.slice(1)}`)
    .join(' ');
}

function formatIssueSummary(issues: ExtractionIssue[]): string {
  return issues.slice(0, 3).map((issue) => issue.message).join(' ');
}

export function buildScrapeStatusMessage(result: ScrapeResult): string {
  const targetLabel = result.jobDetails.jobTitle ?? result.title;
  const attempts = result.extraction?.attempts ?? 1;
  const attemptLabel = attempts === 1 ? '1 attempt' : `${attempts} attempts`;

  if (!result.extraction || result.extraction.status === 'valid') {
    return `Captured ${result.textLength} characters from ${targetLabel} after ${attemptLabel}. Save it to ApplyVault when you are ready.`;
  }

  const issueSummary = formatIssueSummary(result.extraction.issues);
  const statusLabel =
    result.extraction.status === 'partial' ? 'Partial extraction' : 'Low-confidence extraction';

  return `${statusLabel} after ${attemptLabel}. ${issueSummary} You can still save it and let ApplyVault process the record.`;
}

export { formatPageType };

export function setFieldValue(element: EditableControl, value: string | undefined, emptyText: string): void {
  element.value = value?.trim() || '';
  element.placeholder = emptyText;
}
