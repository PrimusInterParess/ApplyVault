import type { FieldExtraction } from '../../domain/extraction/types';
import type { ExtractionContext, ContactsExtractionResult } from '../../domain/extraction/types';

export interface SiteExtractor {
  readonly id: string;
  canHandle(ctx: ExtractionContext): boolean;
  extract(ctx: ExtractionContext): FieldExtraction[];
}

export interface ContactsExtractor {
  extract(ctx: ExtractionContext, hiringSection?: Element): ContactsExtractionResult;
}
