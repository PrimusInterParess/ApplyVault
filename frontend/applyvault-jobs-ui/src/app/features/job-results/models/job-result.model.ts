export interface HiringManagerContact {
  readonly type: string;
  readonly value: string;
  readonly label: string | null;
}

export interface JobDetails {
  readonly sourceHostname: string;
  readonly detectedPageType: string;
  readonly jobTitle: string | null;
  readonly companyName: string | null;
  readonly location: string | null;
  readonly jobDescription: string | null;
  readonly positionSummary: string | null;
  readonly hiringManagerName: string | null;
  readonly hiringManagerContacts: readonly HiringManagerContact[];
}

export interface ScrapeResultPayload {
  readonly title: string;
  readonly url: string;
  readonly text: string;
  readonly textLength: number;
  readonly extractedAt: string;
  readonly jobDetails: JobDetails;
}

export interface SavedJobResult {
  readonly id: string;
  readonly savedAt: string;
  readonly payload: ScrapeResultPayload;
}
