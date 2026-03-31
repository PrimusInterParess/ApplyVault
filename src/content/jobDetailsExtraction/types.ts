import type { HiringManagerContact } from '../../shared/models/scrapeResult';

export interface JsonLdJobPosting {
  title?: string;
  companyName?: string;
  location?: string;
  description?: string;
}

export interface FieldCandidate {
  value: string;
  confidence: number;
  source: string;
}

export interface LinkedInExtractionDetails {
  jobTitle?: string;
  companyName?: string;
  location?: string;
  jobDescription?: string;
  hiringManagerName?: string;
  hiringSection?: Element;
}

export interface LinkedInFeedDetails {
  jobTitle?: string;
  companyName?: string;
  location?: string;
}

export interface GenericExtractionDetails {
  jobTitle?: string;
  companyName?: string;
  location?: string;
  jobDescription?: string;
  hiringManagerName?: string;
}

export interface ContactExtractionOptions {
  hiringSection?: Element;
  restrictLinkedInProfiles?: boolean;
}

export type HiringManagerContacts = HiringManagerContact[];
