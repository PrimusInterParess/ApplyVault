export type HiringManagerContactType = 'email' | 'phone' | 'linkedin' | 'url';

export interface HiringManagerContact {
  type: HiringManagerContactType;
  value: string;
  label?: string;
}

export type DetectedPageType = 'linkedin-job' | 'job-posting' | 'generic-page';

export interface JobDetails {
  sourceHostname: string;
  detectedPageType: DetectedPageType;
  jobTitle?: string;
  companyName?: string;
  location?: string;
  jobDescription?: string;
  positionSummary?: string;
  hiringManagerName?: string;
  hiringManagerContacts: HiringManagerContact[];
}

export interface ScrapeResult {
  title: string;
  url: string;
  text: string;
  textLength: number;
  extractedAt: string;
  jobDetails: JobDetails;
}
