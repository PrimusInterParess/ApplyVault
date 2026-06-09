import { field } from '../../domain/extraction/fieldResolver';
import type { ExtractionContext } from '../../domain/extraction/types';
import type { FieldExtraction } from '../../domain/extraction/types';
import type { SiteExtractor } from './types';

export const jsonLdExtractor: SiteExtractor = {
  id: 'json-ld',

  canHandle(ctx: ExtractionContext): boolean {
    return Boolean(ctx.jsonLdJobPosting);
  },

  extract(ctx: ExtractionContext): FieldExtraction[] {
    const posting = ctx.jsonLdJobPosting;

    if (!posting || ctx.listingIsInactive) {
      return [];
    }

    return [
      ...field(posting.title, 'jobTitle', 0.98, 'json-ld'),
      ...field(posting.companyName, 'companyName', 0.98, 'json-ld'),
      ...field(posting.location, 'location', 0.96, 'json-ld'),
      ...field(posting.description, 'jobDescription', 0.97, 'json-ld')
    ];
  }
};
