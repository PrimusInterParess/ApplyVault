import type { JobDetails } from '../../shared/models/scrapeResult';
import { resolveJobDetails } from '../../domain/extraction/fieldResolver';
import type { ExtractionContext } from '../../domain/extraction/types';
import { extractJsonLdJobPosting } from './jsonLd';
import { contactsExtractor } from '../sites/contactsExtractor';
import { getMetaDescription } from '../sites/metaTagExtractor';
import { detectPageType } from '../sites/pageTypeDetector';
import { collectFieldExtractions } from '../sites/registry';
import { hasInactiveListingSignal } from '../sites/teamtailorSignals';

export function extractJobDetails(documentRef: Document, text: string): JobDetails {
  const textLines = text
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);
  const jsonLdJobPosting = extractJsonLdJobPosting(documentRef);
  const listingIsInactive = hasInactiveListingSignal(documentRef, text);
  const ctx: ExtractionContext = {
    document: documentRef,
    text,
    textLines,
    pageType: detectPageType(documentRef, jsonLdJobPosting),
    jsonLdJobPosting,
    listingIsInactive
  };
  const candidates = collectFieldExtractions(ctx);
  const contacts = contactsExtractor.extract(ctx);
  const { jobDetails } = resolveJobDetails(ctx, candidates, contacts, getMetaDescription(documentRef));

  return jobDetails;
}
