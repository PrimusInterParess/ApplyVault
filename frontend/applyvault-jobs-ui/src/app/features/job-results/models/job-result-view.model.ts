import {
  CalendarEventLink,
  CaptureQuality,
  HiringManagerContact,
  InterviewEvent,
  JobStatusSyncInfo
} from './job-result.model';

export interface JobResultViewModel {
  readonly id: string;
  readonly savedAt: string;
  readonly extractedAt: string;
  readonly isRejected: boolean;
  readonly interviewDate: string | null;
  readonly interviewEvent: InterviewEvent | null;
  readonly calendarEvents: readonly CalendarEventLink[];
  readonly captureQuality: CaptureQuality;
  readonly statusSync: JobStatusSyncInfo | null;
  readonly title: string;
  readonly company: string;
  readonly location: string;
  readonly sourceHostname: string;
  readonly detectedPageType: string;
  readonly summary: string;
  readonly description: string;
  readonly excerpt: string;
  readonly hiringManagerName: string;
  readonly hiringManagerContacts: readonly HiringManagerContact[];
  readonly url: string;
  readonly textLength: number;
  readonly searchText: string;
}

export type { JobWorkflowFilter, JobResultsSortOption } from '../utils/job-result-status.util';
