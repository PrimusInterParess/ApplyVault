import type { ScrapeResult } from '../../shared/models/scrapeResult';

export interface ApiHiringManagerContactPayload {
  type: string;
  value: string;
  label?: string;
}

export interface ApiJobDetailsPayload {
  sourceHostname: string;
  detectedPageType: string;
  jobTitle?: string;
  companyName?: string;
  location?: string;
  jobDescription?: string;
  positionSummary?: string;
  hiringManagerName?: string;
  hiringManagerContacts: ApiHiringManagerContactPayload[];
}

export interface ApiScrapeResultPayload {
  title: string;
  url: string;
  text: string;
  textLength: number;
  extractedAt: string;
  jobDetails: ApiJobDetailsPayload;
}

export function toApiScrapeResultPayload(result: ScrapeResult): ApiScrapeResultPayload {
  const text = result.text;

  return {
    title: result.title,
    url: result.url,
    text,
    textLength: text.length,
    extractedAt: result.extractedAt,
    jobDetails: {
      sourceHostname: result.jobDetails.sourceHostname,
      detectedPageType: result.jobDetails.detectedPageType,
      jobTitle: result.jobDetails.jobTitle,
      companyName: result.jobDetails.companyName,
      location: result.jobDetails.location,
      jobDescription: result.jobDetails.jobDescription,
      positionSummary: result.jobDetails.positionSummary,
      hiringManagerName: result.jobDetails.hiringManagerName,
      hiringManagerContacts: result.jobDetails.hiringManagerContacts.map((contact) => ({
        type: contact.type,
        value: contact.value,
        label: contact.label
      }))
    }
  };
}
