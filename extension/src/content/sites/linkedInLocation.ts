import {
  collectHeaderMetadataCandidates,
  hasStrongLocationSignal,
  isLikelyCompanyValue,
  isLikelyLocationValue
} from '../jobDetailsExtraction/metadata';
import {
  collectSelectorTexts,
  getFirstMatchingText,
  getNormalizedText,
  splitMetadataText
} from '../jobDetailsExtraction/shared';
import { LINKEDIN_COMPANY_SELECTORS, LINKEDIN_JOB_TITLE_SELECTORS, LINKEDIN_LOCATION_SELECTORS } from './linkedin.constants';

function isSameValue(left: string | undefined, right: string | undefined): boolean {
  return Boolean(left && right && left.toLowerCase() === right.toLowerCase());
}

function scoreLinkedInLocationValue(value: string, companyName?: string): number {
  const normalizedValue = getNormalizedText(value);

  if (!normalizedValue || isSameValue(normalizedValue, companyName)) {
    return -1;
  }

  if (!isLikelyLocationValue(normalizedValue)) {
    return -1;
  }

  if (isLikelyCompanyValue(normalizedValue) && !hasStrongLocationSignal(normalizedValue)) {
    return -1;
  }

  let score = 1;

  if (hasStrongLocationSignal(normalizedValue)) {
    score += 100;
  }

  if (/,/.test(normalizedValue)) {
    score += 40;
  }

  if (/\([^)]+\)/.test(normalizedValue)) {
    score += 20;
  }

  score += Math.min(normalizedValue.length, 80) / 10;

  return score;
}

function pickBestLinkedInLocationValue(values: string[], companyName?: string): string | undefined {
  const rankedValues = values
    .flatMap((value) => {
      const normalizedValue = getNormalizedText(value);

      if (!normalizedValue) {
        return [];
      }

      const parts = splitMetadataText(normalizedValue);

      if (parts.length > 1) {
        return parts
          .slice(1)
          .map((part) => ({ value: part, score: scoreLinkedInLocationValue(part, companyName) }));
      }

      return [{ value: normalizedValue, score: scoreLinkedInLocationValue(normalizedValue, companyName) }];
    })
    .filter((entry) => entry.score >= 0)
    .sort((left, right) => right.score - left.score);

  return rankedValues[0]?.value;
}

export function resolveLinkedInLocationText(
  value: string | undefined,
  companyName?: string
): string | undefined {
  return pickBestLinkedInLocationValue(value ? [value] : [], companyName);
}

export function extractLocationAfterTitle(
  textLines: string[],
  title: string,
  companyName?: string
): string | undefined {
  const titleIndex = textLines.findIndex((line) => line === title || line.includes(title));

  if (titleIndex < 0) {
    return undefined;
  }

  const candidateLines = textLines.slice(titleIndex + 1, titleIndex + 6);

  return pickBestLinkedInLocationValue(candidateLines, companyName);
}

export function extractLinkedInLocation(documentRef: Document, textLines: string[]): string | undefined {
  const companyName = getFirstMatchingText(documentRef, LINKEDIN_COMPANY_SELECTORS);
  const selectorValues = collectSelectorTexts(documentRef, LINKEDIN_LOCATION_SELECTORS);
  const selectorLocation = pickBestLinkedInLocationValue(selectorValues, companyName);

  if (selectorLocation) {
    return selectorLocation;
  }

  const jobTitle = getFirstMatchingText(documentRef, LINKEDIN_JOB_TITLE_SELECTORS);
  const headerLocation = pickBestLinkedInLocationValue(
    collectHeaderMetadataCandidates(documentRef, jobTitle, LINKEDIN_JOB_TITLE_SELECTORS)
      .filter((candidate) => candidate.source.includes('location'))
      .map((candidate) => candidate.value),
    companyName
  );

  if (headerLocation) {
    return headerLocation;
  }

  if (jobTitle) {
    return extractLocationAfterTitle(textLines, jobTitle, companyName);
  }

  return undefined;
}
