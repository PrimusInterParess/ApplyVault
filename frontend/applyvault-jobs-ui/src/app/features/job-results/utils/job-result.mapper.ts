import { SavedJobResult } from '../models/job-result.model';
import { JobResultViewModel } from '../models/job-result-view.model';

export function mapSavedJobResultToViewModel(result: SavedJobResult): JobResultViewModel {
  const { payload } = result;
  const title = firstNonEmpty(payload.jobDetails.jobTitle, payload.title, 'Untitled role');
  const company = firstNonEmpty(payload.jobDetails.companyName, extractHostnameLabel(payload.url), 'Unknown company');
  const location = firstNonEmpty(payload.jobDetails.location, 'Location not specified');
  const summary = firstNonEmpty(
    payload.jobDetails.positionSummary,
    payload.jobDetails.jobDescription,
    payload.text,
    'No summary captured for this listing.'
  );
  const description = firstNonEmpty(payload.jobDetails.jobDescription, payload.text, 'No description captured.');
  const excerpt = truncate(summary.replace(/\s+/g, ' ').trim(), 180);
  const sourceHostname = firstNonEmpty(payload.jobDetails.sourceHostname, extractHostnameLabel(payload.url), 'Unknown source');
  const detectedPageType = formatLabel(payload.jobDetails.detectedPageType, 'unknown page');
  const hiringManagerName = firstNonEmpty(payload.jobDetails.hiringManagerName, 'Not detected');
  const searchText = [
    title,
    company,
    location,
    sourceHostname,
    payload.text,
    summary,
    description,
    hiringManagerName
  ]
    .join(' ')
    .toLowerCase();

  return {
    id: result.id,
    savedAt: result.savedAt,
    extractedAt: payload.extractedAt,
    isRejected: result.isRejected,
    title,
    company,
    location,
    sourceHostname,
    detectedPageType,
    summary,
    description,
    excerpt,
    hiringManagerName,
    hiringManagerContacts: payload.jobDetails.hiringManagerContacts,
    url: payload.url,
    textLength: payload.textLength,
    searchText
  };
}

function firstNonEmpty(...values: Array<string | null | undefined>): string {
  for (const value of values) {
    if (value && value.trim().length > 0) {
      return value.trim();
    }
  }

  return '';
}

function extractHostnameLabel(url: string): string {
  try {
    return new URL(url).hostname.replace(/^www\./, '');
  } catch {
    return '';
  }
}

function formatLabel(value: string, fallback: string): string {
  const source = value.trim();

  if (!source) {
    return fallback;
  }

  return source
    .replace(/[-_]+/g, ' ')
    .replace(/\b\w/g, (character) => character.toUpperCase());
}

function truncate(value: string, maxLength: number): string {
  if (value.length <= maxLength) {
    return value;
  }

  return `${value.slice(0, maxLength - 1).trimEnd()}…`;
}
