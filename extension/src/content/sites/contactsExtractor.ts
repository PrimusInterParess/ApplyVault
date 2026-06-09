import type { ContactsExtractionResult, ExtractionContext } from '../../domain/extraction/types';
import {
  extractContacts,
  extractHiringManagerNameFromContacts
} from '../jobDetailsExtraction/contacts';
import { field } from '../../domain/extraction/fieldResolver';
import type { FieldExtraction } from '../../domain/extraction/types';
import { findSectionByHeading } from '../jobDetailsExtraction/shared';
import type { ContactsExtractor, SiteExtractor } from './types';

export const contactsSiteExtractor: SiteExtractor = {
  id: 'contacts',

  canHandle(): boolean {
    return true;
  },

  extract(ctx: ExtractionContext): FieldExtraction[] {
    const hiringManagerName = extractHiringManagerNameFromContacts(
      ctx.document,
      ctx.text,
      extractContactsForContext(ctx).hiringManagerContacts
    );

    return field(hiringManagerName, 'hiringManagerName', 0.88, 'contacts');
  }
};

export const contactsExtractor: ContactsExtractor = {
  extract(ctx: ExtractionContext): ContactsExtractionResult {
    return extractContactsForContext(ctx);
  }
};

function extractContactsForContext(ctx: ExtractionContext): ContactsExtractionResult {
  const hiringSection =
    ctx.pageType === 'linkedin-job'
      ? findSectionByHeading(ctx.document, ['meet the hiring team', 'hiring team'])
      : undefined;

  return {
    hiringManagerContacts: extractContacts(ctx.document, ctx.text, {
      hiringSection,
      restrictLinkedInProfiles: ctx.document.location.hostname.toLowerCase().includes('linkedin.com')
    }),
    hiringSection
  };
}
