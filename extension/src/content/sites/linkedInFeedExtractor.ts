import { field } from '../../domain/extraction/fieldResolver';
import type { ExtractionContext } from '../../domain/extraction/types';
import type { FieldExtraction } from '../../domain/extraction/types';
import { isLikelyCompanyValue } from '../jobDetailsExtraction/metadata';
import { getTextFromElement, splitMetadataText } from '../jobDetailsExtraction/shared';
import {
  LINKEDIN_ACTIVE_JOB_CARD_SELECTORS,
  LINKEDIN_FEED_JOB_CARD_SELECTORS
} from './linkedin.constants';
import { resolveLinkedInLocationText } from './linkedInLocation';
import type { SiteExtractor } from './types';

function getJobCardAnchors(documentRef: Document): HTMLAnchorElement[] {
  const activeCards = Array.from(
    documentRef.querySelectorAll<HTMLAnchorElement>(LINKEDIN_ACTIVE_JOB_CARD_SELECTORS.join(', '))
  );

  if (activeCards.length > 0) {
    return activeCards;
  }

  return Array.from(
    documentRef.querySelectorAll<HTMLAnchorElement>(LINKEDIN_FEED_JOB_CARD_SELECTORS.join(', '))
  );
}

function extractCardMetadata(
  title: string,
  paragraphTexts: string[]
): { companyName?: string; location?: string } {
  const metadataTexts = paragraphTexts.filter(
    (value) => value !== title && !value.includes(title) && !title.includes(value)
  );

  let companyName: string | undefined;
  let location: string | undefined;

  for (const metadataText of metadataTexts) {
    const parts = splitMetadataText(metadataText);

    if (parts.length > 1) {
      if (!companyName && isLikelyCompanyValue(parts[0])) {
        companyName = parts[0];
      }

      for (const part of parts.slice(1)) {
        if (!location) {
          location = resolveLinkedInLocationText(part, companyName);
        }
      }

      continue;
    }

    if (!companyName && isLikelyCompanyValue(metadataText)) {
      companyName = metadataText;
      continue;
    }

    if (!location) {
      location = resolveLinkedInLocationText(metadataText, companyName);
    }
  }

  return { companyName, location };
}

export const linkedInFeedExtractor: SiteExtractor = {
  id: 'linkedin-feed',

  canHandle(ctx: ExtractionContext): boolean {
    const hostname = ctx.document.location.hostname.toLowerCase();
    return hostname.includes('linkedin.com') && ctx.document.location.pathname.toLowerCase().includes('/jobs/');
  },

  extract(ctx: ExtractionContext): FieldExtraction[] {
    const candidates = getJobCardAnchors(ctx.document);

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

      const { companyName, location } = extractCardMetadata(title, paragraphTexts);

      if (title || companyName || location) {
        return [
          ...field(title, 'jobTitle', 0.88, 'linkedin-feed'),
          ...field(companyName, 'companyName', 0.86, 'linkedin-feed'),
          ...field(location, 'location', 0.84, 'linkedin-feed')
        ];
      }
    }

    return [];
  }
};
