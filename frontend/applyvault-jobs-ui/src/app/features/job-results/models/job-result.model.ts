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

export interface CaptureQualityField {
  readonly originalValue: string | null;
  readonly effectiveValue: string | null;
  readonly userOverrideValue: string | null;
  readonly confidence: number;
  readonly needsReview: boolean;
  readonly reviewReason: string | null;
}

export interface CaptureQuality {
  readonly reviewStatus: 'not_required' | 'needs_review' | 'reviewed';
  readonly needsReview: boolean;
  readonly overallConfidence: number;
  readonly jobTitle: CaptureQualityField;
  readonly companyName: CaptureQualityField;
  readonly location: CaptureQualityField;
  readonly jobDescription: CaptureQualityField;
}

export interface SavedJobResult {
  readonly id: string;
  readonly savedAt: string;
  readonly isRejected: boolean;
  readonly interviewDate: string | null;
  readonly interviewEvent: InterviewEvent | null;
  readonly calendarEvents: readonly CalendarEventLink[];
  readonly payload: ScrapeResultPayload;
  readonly captureQuality?: CaptureQuality;
  readonly statusSync: JobStatusSyncInfo | null;
}

export interface UpdateJobDescriptionRequest {
  readonly description: string;
}

export interface UpdateJobCaptureReviewRequest {
  readonly jobTitle: string | null;
  readonly companyName: string | null;
  readonly location: string | null;
  readonly jobDescription: string | null;
}

export interface UpdateJobInterviewDateRequest {
  readonly interviewDate: string | null;
}

export interface InterviewEvent {
  readonly startUtc: string;
  readonly endUtc: string;
  readonly timeZone: string;
  readonly location: string | null;
  readonly notes: string | null;
}

export interface UpdateInterviewEventRequest {
  readonly startUtc: string;
  readonly endUtc: string;
  readonly timeZone: string;
  readonly location: string | null;
  readonly notes: string | null;
}

export interface CalendarEventLink {
  readonly id: string;
  readonly connectedAccountId: string;
  readonly provider: string;
  readonly externalEventId: string;
  readonly externalEventUrl: string | null;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface CreateCalendarEventRequest {
  readonly connectedAccountId: string;
}

export interface JobStatusSyncInfo {
  readonly source: 'manual' | 'gmail' | string;
  readonly kind: 'rejection' | 'interview' | string;
  readonly updatedAt: string;
  readonly emailReceivedAt: string | null;
  readonly emailFrom: string | null;
  readonly emailSubject: string | null;
}
