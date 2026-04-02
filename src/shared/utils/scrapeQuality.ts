import type { ExtractionIssue, ExtractionMetadata, ScrapeResult } from '../models/scrapeResult';
import { normalizeWhitespace } from './textCleanup';

const MIN_DESCRIPTION_LENGTH = 120;
const MIN_TEXT_LENGTH = 200;
const GENERIC_TITLE_PATTERNS = [
  /^jobs?$/i,
  /^job search$/i,
  /^linkedin$/i,
  /^workday$/i,
  /^greenhouse$/i,
  /^lever$/i,
  /^apply(?: now)?$/i,
  /^careers?$/i
];

function normalizeOptionalValue(value: string | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const normalizedValue = normalizeWhitespace(value);
  return normalizedValue.length > 0 ? normalizedValue : undefined;
}

function isGenericTitle(value: string | undefined): boolean {
  if (!value) {
    return false;
  }

  return GENERIC_TITLE_PATTERNS.some((pattern) => pattern.test(value));
}

function createIssue(field: ExtractionIssue['field'], code: string, message: string): ExtractionIssue {
  return { field, code, message };
}

function getIssuePenalty(issue: ExtractionIssue): number {
  switch (issue.code) {
    case 'missing-title':
    case 'missing-description':
      return 200;
    case 'missing-company':
      return 120;
    case 'generic-title':
    case 'short-description':
      return 90;
    case 'short-page-text':
      return 80;
    default:
      return 40;
  }
}

export function evaluateScrapeResult(result: ScrapeResult): ExtractionMetadata {
  const issues: ExtractionIssue[] = [];
  const normalizedTitle = normalizeOptionalValue(result.jobDetails.jobTitle);
  const normalizedCompanyName = normalizeOptionalValue(result.jobDetails.companyName);
  const normalizedDescription = normalizeOptionalValue(result.jobDetails.jobDescription);
  const normalizedText = normalizeOptionalValue(result.text);

  if (!normalizedTitle) {
    issues.push(createIssue('title', 'missing-title', 'Job title is missing.'));
  } else if (isGenericTitle(normalizedTitle)) {
    issues.push(createIssue('title', 'generic-title', 'Job title still looks generic and may need review.'));
  }

  if (!normalizedCompanyName) {
    issues.push(createIssue('companyName', 'missing-company', 'Company name was not found.'));
  }

  if (!normalizedDescription) {
    issues.push(createIssue('jobDescription', 'missing-description', 'Job description was not captured.'));
  } else if (normalizedDescription.length < MIN_DESCRIPTION_LENGTH) {
    issues.push(
      createIssue(
        'jobDescription',
        'short-description',
        'Job description is unusually short and may be incomplete.'
      )
    );
  }

  if (!normalizedText || normalizedText.length < MIN_TEXT_LENGTH) {
    issues.push(createIssue('text', 'short-page-text', 'Visible page text is unusually short.'));
  }

  const status =
    (!normalizedTitle && !normalizedCompanyName && !normalizedDescription) ||
    (!normalizedDescription && (!normalizedText || normalizedText.length < MIN_TEXT_LENGTH))
      ? 'invalid'
      : issues.length > 0
        ? 'partial'
        : 'valid';

  return {
    status,
    issues,
    attempts: result.extraction?.attempts ?? 1
  };
}

export function getScrapeResultScore(result: ScrapeResult): number {
  const extraction = result.extraction ?? evaluateScrapeResult(result);
  const descriptionLength = normalizeOptionalValue(result.jobDetails.jobDescription)?.length ?? 0;
  const titleLength = normalizeOptionalValue(result.jobDetails.jobTitle)?.length ?? 0;
  const companyLength = normalizeOptionalValue(result.jobDetails.companyName)?.length ?? 0;
  const baseScore =
    extraction.status === 'valid' ? 1000 : extraction.status === 'partial' ? 500 : 0;
  const penalty = extraction.issues.reduce((total, issue) => total + getIssuePenalty(issue), 0);

  return (
    baseScore +
    Math.min(result.textLength, 4000) / 4 +
    Math.min(descriptionLength, 2000) / 2 +
    Math.min(titleLength, 120) +
    Math.min(companyLength, 120) -
    penalty
  );
}
