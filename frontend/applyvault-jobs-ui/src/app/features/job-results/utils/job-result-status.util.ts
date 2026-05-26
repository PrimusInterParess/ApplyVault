import { CaptureQuality, InterviewEvent, JobStatusSyncInfo } from '../models/job-result.model';
import { JobResultViewModel } from '../models/job-result-view.model';

export type JobWorkflowFilter = 'all' | 'needs_review' | 'interview' | 'rejected' | 'hide_rejected';

export type JobResultsSortOption = 'saved_desc' | 'title_asc' | 'company_asc' | 'interview_asc';

export interface JobCardStatus {
  readonly label: string;
  readonly variant: 'review' | 'interview' | 'rejected' | 'gmail';
  readonly overflowCount: number;
}

export function countCaptureFieldsNeedingReview(captureQuality: CaptureQuality): number {
  const fields = [
    captureQuality.jobTitle,
    captureQuality.companyName,
    captureQuality.location,
    captureQuality.jobDescription
  ];

  return fields.filter((field) => field.needsReview).length;
}

export function describeCaptureQualitySummary(captureQuality: CaptureQuality): string {
  const fieldsNeedingReview = countCaptureFieldsNeedingReview(captureQuality);

  if (captureQuality.reviewStatus === 'reviewed' && fieldsNeedingReview === 0) {
    return 'Auto-captured — reviewed';
  }

  if (fieldsNeedingReview > 0) {
    const fieldLabel = fieldsNeedingReview === 1 ? 'field needs' : 'fields need';
    return `Auto-captured — ${fieldsNeedingReview} ${fieldLabel} your review`;
  }

  return 'Auto-captured from listing page';
}

export function formatRelativeInterviewDate(interviewEvent: InterviewEvent | null): string {
  if (!interviewEvent) {
    return '';
  }

  const start = new Date(interviewEvent.startUtc);
  const now = new Date();
  const startDay = new Date(start.getFullYear(), start.getMonth(), start.getDate());
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const dayDiff = Math.round((startDay.getTime() - today.getTime()) / 86_400_000);

  const timeFormatter = new Intl.DateTimeFormat(undefined, {
    hour: 'numeric',
    minute: '2-digit'
  });
  const timeLabel = timeFormatter.format(start);

  if (dayDiff === 0) {
    return `Today ${timeLabel}`;
  }

  if (dayDiff === 1) {
    return `Tomorrow ${timeLabel}`;
  }

  if (dayDiff === -1) {
    return `Yesterday ${timeLabel}`;
  }

  const dateFormatter = new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit'
  });

  return dateFormatter.format(start);
}

export function sourceMonogram(sourceHostname: string): string {
  const trimmed = sourceHostname.trim();

  if (!trimmed) {
    return '?';
  }

  const firstChar = trimmed.replace(/^www\./i, '').charAt(0);

  return firstChar ? firstChar.toUpperCase() : '?';
}

export function resolveJobCardStatus(job: JobResultViewModel): JobCardStatus | null {
  const statuses: JobCardStatus[] = [];

  if (job.captureQuality.needsReview) {
    statuses.push({ label: 'Needs review', variant: 'review', overflowCount: 0 });
  }

  if (job.interviewEvent) {
    const relative = formatRelativeInterviewDate(job.interviewEvent);
    statuses.push({
      label: relative ? `Interview ${relative}` : 'Interview scheduled',
      variant: 'interview',
      overflowCount: 0
    });
  }

  if (job.isRejected) {
    statuses.push({ label: 'Rejected', variant: 'rejected', overflowCount: 0 });
  }

  const gmailLabel = gmailSyncLabel(job.statusSync);

  if (gmailLabel) {
    statuses.push({ label: gmailLabel, variant: 'gmail', overflowCount: 0 });
  }

  if (statuses.length === 0) {
    return null;
  }

  const primary = statuses[0];

  return {
    ...primary,
    overflowCount: Math.max(0, statuses.length - 1)
  };
}

function gmailSyncLabel(statusSync: JobStatusSyncInfo | null): string | null {
  if (!statusSync || statusSync.source !== 'gmail') {
    return null;
  }

  return statusSync.kind === 'interview' ? 'Gmail interview' : 'Gmail rejected';
}

export function matchesWorkflowFilter(job: JobResultViewModel, filter: JobWorkflowFilter): boolean {
  switch (filter) {
    case 'all':
      return true;
    case 'needs_review':
      return job.captureQuality.needsReview;
    case 'interview':
      return job.interviewEvent !== null;
    case 'rejected':
      return job.isRejected;
    case 'hide_rejected':
      return !job.isRejected;
  }
}

export function compareJobResults(
  left: JobResultViewModel,
  right: JobResultViewModel,
  sort: JobResultsSortOption
): number {
  switch (sort) {
    case 'title_asc':
      return left.title.localeCompare(right.title);
    case 'company_asc':
      return left.company.localeCompare(right.company);
    case 'interview_asc': {
      const leftTime = left.interviewEvent ? new Date(left.interviewEvent.startUtc).getTime() : Number.MAX_SAFE_INTEGER;
      const rightTime = right.interviewEvent
        ? new Date(right.interviewEvent.startUtc).getTime()
        : Number.MAX_SAFE_INTEGER;
      return leftTime - rightTime;
    }
    case 'saved_desc':
    default:
      return new Date(right.savedAt).getTime() - new Date(left.savedAt).getTime();
  }
}
