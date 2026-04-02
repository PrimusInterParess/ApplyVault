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
  readonly interviewEvent: InterviewEvent | null;
  readonly calendarEvents: readonly CalendarEventLink[];
  readonly payload: ScrapeResultPayload;
}

export interface UpdateJobDescriptionRequest {
  readonly description: string;
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

export interface ConnectedCalendarAccount {
  readonly id: string;
  readonly provider: string;
  readonly providerUserId: string;
  readonly email: string | null;
  readonly displayName: string | null;
  readonly expiresAt: string | null;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface CalendarAuthorizationStartResponse {
  readonly authorizationUrl: string;
}

export interface CreateCalendarEventRequest {
  readonly connectedAccountId: string;
}
