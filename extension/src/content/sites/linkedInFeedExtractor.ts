import { field } from '../../domain/extraction/fieldResolver';
import type { ExtractionContext } from '../../domain/extraction/types';
import type { FieldExtraction } from '../../domain/extraction/types';
import { getNormalizedText, getTextFromElement } from '../jobDetailsExtraction/shared';
import { LINKEDIN_FEED_JOB_CARD_SELECTORS } from './linkedin.constants';
import type { SiteExtractor } from './types';

export const linkedInFeedExtractor: SiteExtractor = {
  id: 'linkedin-feed',

  canHandle(ctx: ExtractionContext): boolean {
    const hostname = ctx.document.location.hostname.toLowerCase();
    return hostname.includes('linkedin.com') && ctx.document.location.pathname.toLowerCase().includes('/jobs/');
  },

  extract(ctx: ExtractionContext): FieldExtraction[] {
    const candidates = Array.from(
      ctx.document.querySelectorAll<HTMLAnchorElement>(LINKEDIN_FEED_JOB_CARD_SELECTORS.join(', '))
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
