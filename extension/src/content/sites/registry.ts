import type { ExtractionContext } from '../../domain/extraction/types';
import type { FieldExtraction } from '../../domain/extraction/types';
import { contactsSiteExtractor } from './contactsExtractor';
import { genericExtractor } from './genericExtractor';
import { jsonLdExtractor } from './jsonLdExtractor';
import { linkedInFeedExtractor } from './linkedInFeedExtractor';
import { linkedInJobExtractor } from './linkedInJobExtractor';
import { metaTagExtractor } from './metaTagExtractor';
import type { SiteExtractor } from './types';

const SITE_EXTRACTORS: SiteExtractor[] = [
  linkedInJobExtractor,
  linkedInFeedExtractor,
  jsonLdExtractor,
  genericExtractor,
  metaTagExtractor,
  contactsSiteExtractor
];

export function collectFieldExtractions(ctx: ExtractionContext): FieldExtraction[] {
  const candidates: FieldExtraction[] = [];

  for (const extractor of SITE_EXTRACTORS) {
    if (!extractor.canHandle(ctx)) {
      continue;
    }

    candidates.push(...extractor.extract(ctx));
  }

  return candidates;
}

export { SITE_EXTRACTORS };
