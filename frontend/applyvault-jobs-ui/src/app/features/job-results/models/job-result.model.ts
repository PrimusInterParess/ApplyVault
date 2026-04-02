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
  readonly isRejected: boolean;
  readonly interviewDate: string | null;
  readonly payload: ScrapeResultPayload;
}

export interface UpdateJobDescriptionRequest {
  readonly description: string;
}

export interface UpdateJobInterviewDateRequest {
  readonly interviewDate: string | null;
}
