import type { ExtractionIssue, HiringManagerContact, JobDetails, ScrapeResult } from '../shared/models/scrapeResult';
import { evaluateScrapeResult } from '../shared/utils/scrapeQuality';
import type { EditableControl, PopupDetailsElements } from './popupTypes';

export function getOptionalValue(value: string): string | undefined {
  const trimmedValue = value.trim();
  return trimmedValue.length > 0 ? trimmedValue : undefined;
}

function formatPageType(pageType: JobDetails['detectedPageType']): string {
  return pageType
    .split('-')
    .map((part) => `${part.charAt(0).toUpperCase()}${part.slice(1)}`)
    .join(' ');
}

function parsePageType(
  value: string,
  fallbackPageType: JobDetails['detectedPageType']
): JobDetails['detectedPageType'] {
  const normalizedValue = value.trim().toLowerCase().replace(/\s+/g, '-');

  if (
    normalizedValue === 'linkedin-job' ||
    normalizedValue === 'job-posting' ||
    normalizedValue === 'generic-page'
  ) {
    return normalizedValue;
  }

  return fallbackPageType;
}

function parseContactLine(line: string): HiringManagerContact | null {
  const match = /^(?:(.+?):\s*)?(email|phone|linkedin|url)\s*-\s*(.+)$/i.exec(line.trim());

  if (!match) {
    return null;
  }

  const [, label, type, value] = match;

  return {
    type: type.toLowerCase() as HiringManagerContact['type'],
    value: value.trim(),
    label: label?.trim() || undefined
  };
}

export function parseContacts(value: string, fallbackContacts: HiringManagerContact[]): HiringManagerContact[] {
  const lines = value
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0);

  if (lines.length === 0) {
    return [];
  }

  const parsedContacts = lines.map(parseContactLine);

  if (parsedContacts.some((contact) => contact === null)) {
    return fallbackContacts;
  }

  return parsedContacts as HiringManagerContact[];
}

function formatIssueSummary(issues: ExtractionIssue[]): string {
  return issues.slice(0, 3).map((issue) => issue.message).join(' ');
}

export function buildScrapeStatusMessage(result: ScrapeResult): string {
  const targetLabel = result.jobDetails.jobTitle ?? result.title;
  const attempts = result.extraction?.attempts ?? 1;
  const attemptLabel = attempts === 1 ? '1 attempt' : `${attempts} attempts`;

  if (!result.extraction || result.extraction.status === 'valid') {
    return `Captured ${result.textLength} characters from ${targetLabel} after ${attemptLabel}. Review the data, then save it to the API.`;
  }

  const issueSummary = formatIssueSummary(result.extraction.issues);
  const statusLabel =
    result.extraction.status === 'partial' ? 'Partial extraction' : 'Low-confidence extraction';

  return `${statusLabel} after ${attemptLabel}. ${issueSummary} Review the extracted fields before saving.`;
}

export function formatFieldSourceHint(fieldSources: JobDetails['fieldSources'], field: keyof NonNullable<JobDetails['fieldSources']>): string {
  const source = fieldSources?.[field];
  return source ? `source: ${source}` : '';
}

export { formatPageType };

export function buildScrapeResultForSave(
  originalResult: ScrapeResult,
  details: PopupDetailsElements,
  textArea: HTMLTextAreaElement,
  descriptionArea: HTMLTextAreaElement
): ScrapeResult {
  const text = textArea.value;
  const updatedResult: ScrapeResult = {
    ...originalResult,
    text,
    textLength: text.length,
    extractedAt: getOptionalValue(details.scrapedAt.value) ?? originalResult.extractedAt,
    jobDetails: {
      ...originalResult.jobDetails,
      sourceHostname:
        getOptionalValue(details.sourceHostname.value) ?? originalResult.jobDetails.sourceHostname,
      detectedPageType: parsePageType(
        details.pageType.value,
        originalResult.jobDetails.detectedPageType
      ),
      jobTitle: getOptionalValue(details.jobTitle.value),
      companyName: getOptionalValue(details.companyName.value),
      location: getOptionalValue(details.jobLocation.value),
      jobDescription: getOptionalValue(descriptionArea.value),
      positionSummary: getOptionalValue(details.positionSummary.value),
      hiringManagerName: getOptionalValue(details.hiringManager.value),
      hiringManagerContacts: parseContacts(
        details.contacts.value,
        originalResult.jobDetails.hiringManagerContacts
      )
    }
  };

  return {
    ...updatedResult,
    extraction: {
      ...evaluateScrapeResult(updatedResult),
      attempts: originalResult.extraction?.attempts ?? 1
    }
  };
}

export function setFieldValue(element: EditableControl, value: string | undefined, emptyText: string): void {
  element.value = value?.trim() || '';
  element.placeholder = emptyText;
}
