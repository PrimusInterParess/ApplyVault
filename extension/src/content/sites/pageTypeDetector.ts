import type { DetectedPageType } from '../../shared/models/scrapeResult';
import { DESCRIPTION_LABELS } from '../dom/constants';
import {
  getFirstMatchingText,
  findSectionByHeading,
  getTextFromElement
} from '../jobDetailsExtraction/shared';
import type { JsonLdJobPosting } from '../jobDetailsExtraction/types';
import {
  GENERIC_DESCRIPTION_SELECTORS,
  GENERIC_TITLE_SELECTORS
} from './generic.constants';
import {
  HIRING_MANAGER_SELECTORS,
  LINKEDIN_DESCRIPTION_SELECTORS,
  LINKEDIN_JOB_TITLE_SELECTORS
} from './linkedin.constants';

function isLinkedInJobDetailPage(documentRef: Document): boolean {
  const pathname = documentRef.location.pathname.toLowerCase();

  if (pathname.includes('/jobs/view/')) {
    return true;
  }

  const hasJobTitle = Boolean(getFirstMatchingText(documentRef, LINKEDIN_JOB_TITLE_SELECTORS));
  const hasDescription = Boolean(
    getFirstMatchingText(documentRef, LINKEDIN_DESCRIPTION_SELECTORS) ||
      findSectionByHeading(documentRef, DESCRIPTION_LABELS)
  );
  const hasHiringTeam = Boolean(
    documentRef.querySelector(HIRING_MANAGER_SELECTORS.join(', ')) ||
      findSectionByHeading(documentRef, ['meet the hiring team', 'hiring team'])
  );

  return hasJobTitle && (hasDescription || hasHiringTeam);
}

function hasGenericJobSignals(documentRef: Document): boolean {
  const normalizedPath = documentRef.location.pathname.toLowerCase().replace(/[-_/]+/g, ' ');

  if (/\b(job|jobs|career|careers|position|vacanc(?:y|ies)|opening|opportunity|role)\b/.test(normalizedPath)) {
    return true;
  }

  const hasTitle = Boolean(getFirstMatchingText(documentRef, GENERIC_TITLE_SELECTORS));
  const hasDescription = Boolean(
    documentRef.querySelector(GENERIC_DESCRIPTION_SELECTORS.join(', ')) ||
      findSectionByHeading(documentRef, DESCRIPTION_LABELS)
  );
  const hasApplyAction = Array.from(documentRef.querySelectorAll('a, button')).some((element) => {
    const text = getTextFromElement(element)?.toLowerCase();
    return Boolean(text && /\b(apply|apply now|ansøg|ansog)\b/.test(text));
  });

  return hasTitle && (hasDescription || hasApplyAction);
}

export function detectPageType(
  documentRef: Document,
  jsonLdJobPosting?: JsonLdJobPosting
): DetectedPageType {
  const hostname = documentRef.location.hostname.toLowerCase();

  if (hostname.includes('linkedin.com') && isLinkedInJobDetailPage(documentRef)) {
    return 'linkedin-job';
  }

  if (jsonLdJobPosting) {
    return 'job-posting';
  }

  if (hasGenericJobSignals(documentRef)) {
    return 'job-posting';
  }

  return 'generic-page';
}
