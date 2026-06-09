export type HiringManagerContactType = 'email' | 'phone' | 'linkedin' | 'url';

export type ExtractionStatus = 'valid' | 'partial' | 'invalid';

export type ExtractionIssueField = 'title' | 'companyName' | 'jobDescription' | 'text';

export interface ExtractionIssue {
  field: ExtractionIssueField;
  code: string;
  message: string;
}

export interface ExtractionMetadata {
  status: ExtractionStatus;
  issues: ExtractionIssue[];
  attempts: number;
}

export interface HiringManagerContact {
  type: HiringManagerContactType;
  value: string;
  label?: string;
}

export type DetectedPageType = 'linkedin-job' | 'job-posting' | 'generic-page';

export type JobField =
  | 'jobTitle'
  | 'companyName'
  | 'location'
  | 'jobDescription'
  | 'positionSummary'
  | 'hiringManagerName';

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
  fieldSources?: Partial<Record<JobField, string>>;
  extractorVersion?: string;
}

export interface ScrapeResult {
  title: string;
  url: string;
  text: string;
  textLength: number;
  extractedAt: string;
  jobDetails: JobDetails;
  extraction?: ExtractionMetadata;
}
