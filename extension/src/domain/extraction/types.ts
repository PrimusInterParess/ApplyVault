import type { DetectedPageType, HiringManagerContact, JobDetails } from '../../shared/models/scrapeResult';
import type { JsonLdJobPosting } from '../../content/jobDetailsExtraction/types';

export type JobField =
  | 'jobTitle'
  | 'companyName'
  | 'location'
  | 'jobDescription'
  | 'positionSummary'
  | 'hiringManagerName';

export interface FieldExtraction {
  field: JobField;
  value: string;
  confidence: number;
  source: string;
}

export interface ExtractionContext {
  document: Document;
  text: string;
  textLines: string[];
  pageType: DetectedPageType;
  jsonLdJobPosting?: JsonLdJobPosting;
  listingIsInactive: boolean;
}

export interface ResolvedJobDetails {
  jobDetails: JobDetails;
  fieldSources: Partial<Record<JobField, string>>;
}

export const EXTRACTOR_VERSION = '2.0.0';

export type ContactsExtractionResult = {
  hiringManagerContacts: HiringManagerContact[];
  hiringSection?: Element;
};
