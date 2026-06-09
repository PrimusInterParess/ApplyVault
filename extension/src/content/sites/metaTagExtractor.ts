import { field } from '../../domain/extraction/fieldResolver';
import type { ExtractionContext } from '../../domain/extraction/types';
import type { FieldExtraction } from '../../domain/extraction/types';
import { getMetaContent } from '../jobDetailsExtraction/shared';
import type { SiteExtractor } from './types';

export function getMetaDescription(documentRef: Document): string | undefined {
  return (
    getMetaContent(documentRef, 'description') ??
    getMetaContent(documentRef, 'og:description') ??
    getMetaContent(documentRef, 'twitter:description')
  );
}

export const metaTagExtractor: SiteExtractor = {
  id: 'meta-tag',

  canHandle(): boolean {
    return true;
  },

  extract(ctx: ExtractionContext): FieldExtraction[] {
    const { document, listingIsInactive } = ctx;
    const metaTitle = getMetaContent(document, 'og:title') ?? getMetaContent(document, 'twitter:title');
    const metaDescription = getMetaDescription(document);

    return [
      ...field(metaTitle, 'jobTitle', 0.72, 'meta-tag'),
      ...field(getMetaContent(document, 'og:site_name'), 'companyName', 0.7, 'meta-tag'),
      ...field(listingIsInactive ? undefined : metaDescription, 'positionSummary', 0.68, 'meta-tag')
    ];
  }
};
