import type { DetectedPageType } from '../../shared/models/scrapeResult';
import {
  COMPANY_LABELS,
  DESCRIPTION_LABELS,
  GENERIC_COMPANY_SELECTORS,
  GENERIC_DESCRIPTION_SELECTORS,
  GENERIC_LOCATION_SELECTORS,
  GENERIC_TITLE_SELECTORS,
  HIRING_MANAGER_SELECTORS,
  LINKEDIN_COMPANY_SELECTORS,
  LINKEDIN_DESCRIPTION_SELECTORS,
  LINKEDIN_FEED_JOB_CARD_SELECTORS,
  LINKEDIN_JOB_TITLE_SELECTORS,
  LINKEDIN_LOCATION_SELECTORS,
  LOCATION_LABELS
} from './constants';
import {
  extractHiringManagerName,
  extractHiringManagerNameFromSection
} from './contacts';
import {
  getBestGenericDescription,
  getDescriptionFromSection,
  getFirstMatchingDescription
} from './description';
import {
  collectHeaderMetadataCandidates,
  isLikelyCompanyValue,
  isLikelyLocationValue
} from './metadata';
import {
  addFieldCandidate,
  collectSelectorTexts,
  extractLabeledValue,
  extractLabeledValueFromElements,
  findSectionByHeading,
  getFirstMatchingText,
  getNormalizedText,
  getTextFromElement,
  pickBestCandidate
} from './shared';
import type {
  FieldCandidate,
  GenericExtractionDetails,
  JsonLdJobPosting,
  LinkedInExtractionDetails,
  LinkedInFeedDetails
} from './types';

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

export function extractLinkedInDetails(
  documentRef: Document,
  textLines: string[]
): LinkedInExtractionDetails {
  const descriptionText = getFirstMatchingDescription(documentRef, LINKEDIN_DESCRIPTION_SELECTORS);
  const fallbackDescriptionSection = findSectionByHeading(documentRef, DESCRIPTION_LABELS);
  const hiringSection = findSectionByHeading(documentRef, ['meet the hiring team', 'hiring team']);

  return {
    jobTitle: getFirstMatchingText(documentRef, LINKEDIN_JOB_TITLE_SELECTORS),
    companyName: getFirstMatchingText(documentRef, LINKEDIN_COMPANY_SELECTORS),
    location: getFirstMatchingText(documentRef, LINKEDIN_LOCATION_SELECTORS),
    jobDescription: descriptionText ?? getDescriptionFromSection(fallbackDescriptionSection),
    hiringManagerName:
      extractHiringManagerNameFromSection(hiringSection) ?? extractHiringManagerName(documentRef, textLines),
    hiringSection
  };
}

export function extractLinkedInFeedDetails(documentRef: Document): LinkedInFeedDetails | undefined {
  const candidates = Array.from(
    documentRef.querySelectorAll<HTMLAnchorElement>(LINKEDIN_FEED_JOB_CARD_SELECTORS.join(', '))
  );

  for (const candidate of candidates) {
    const title =
      getTextFromElement(candidate.querySelector('p span[aria-hidden="true"]')) ??
      getTextFromElement(candidate.querySelector('p'));

    if (!title) {
      continue;
    }

    const paragraphTexts = Array.from(candidate.querySelectorAll('p'))
      .map((paragraph) => getTextFromElement(paragraph))
      .filter((value): value is string => Boolean(value));

    const metadataTexts = paragraphTexts.filter(
      (value) => value !== title && !value.includes(title) && !title.includes(value)
    );

    let companyName: string | undefined;
    let location: string | undefined;

    for (const metadataText of metadataTexts) {
      const parts = metadataText
        .split('•')
        .map((part) => getNormalizedText(part))
        .filter((part): part is string => Boolean(part));

      if (!companyName && parts[0]) {
        companyName = parts[0];
      }

      if (!location && parts[1]) {
        location = parts[1];
      }
    }

    if (title || companyName || location) {
      return {
        jobTitle: title,
        companyName,
        location
      };
    }
  }

  return undefined;
}

export function extractGenericDetails(
  documentRef: Document,
  textLines: string[]
): GenericExtractionDetails {
  const descriptionSection = findSectionByHeading(documentRef, DESCRIPTION_LABELS);
  const jobTitleCandidates: FieldCandidate[] = [];
  const companyCandidates: FieldCandidate[] = [];
  const locationCandidates: FieldCandidate[] = [];
  const primaryTitle = getFirstMatchingText(documentRef, GENERIC_TITLE_SELECTORS);

  addFieldCandidate(jobTitleCandidates, primaryTitle, 0.96, 'generic-title');
  addFieldCandidate(
    companyCandidates,
    extractLabeledValueFromElements(documentRef, COMPANY_LABELS),
    0.95,
    'company-label-element',
    isLikelyCompanyValue
  );
  addFieldCandidate(
    companyCandidates,
    extractLabeledValue(textLines, COMPANY_LABELS),
    0.9,
    'company-label-line',
    isLikelyCompanyValue
  );
  addFieldCandidate(
    locationCandidates,
    extractLabeledValueFromElements(documentRef, LOCATION_LABELS),
    0.98,
    'location-label-element',
    isLikelyLocationValue
  );
  addFieldCandidate(
    locationCandidates,
    extractLabeledValue(textLines, LOCATION_LABELS),
    0.92,
    'location-label-line',
    isLikelyLocationValue
  );

  for (const text of collectSelectorTexts(documentRef, GENERIC_COMPANY_SELECTORS)) {
    addFieldCandidate(companyCandidates, text, 0.76, 'company-selector', isLikelyCompanyValue);
  }

  for (const text of collectSelectorTexts(documentRef, GENERIC_LOCATION_SELECTORS)) {
    addFieldCandidate(locationCandidates, text, 0.8, 'location-selector', isLikelyLocationValue);
  }

  for (const candidate of collectHeaderMetadataCandidates(documentRef, primaryTitle)) {
    if (candidate.source.includes('company')) {
      addFieldCandidate(companyCandidates, candidate.value, candidate.confidence, candidate.source, isLikelyCompanyValue);
      continue;
    }

    addFieldCandidate(locationCandidates, candidate.value, candidate.confidence, candidate.source, isLikelyLocationValue);
  }

  return {
    jobTitle: pickBestCandidate(jobTitleCandidates),
    companyName: pickBestCandidate(companyCandidates),
    location: pickBestCandidate(locationCandidates),
    jobDescription:
      getBestGenericDescription(documentRef) ??
      getDescriptionFromSection(descriptionSection) ??
      extractLabeledValue(textLines, DESCRIPTION_LABELS),
    hiringManagerName: extractHiringManagerName(documentRef, textLines)
  };
}
