import { InterviewEvent } from '../models/job-result.model';

export function formatInterviewEventWindow(interviewEvent: InterviewEvent | null): string {
  if (!interviewEvent) {
    return 'Not scheduled';
  }

  const formatter = new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit'
  });

  return formatter.format(new Date(interviewEvent.startUtc));
}

export function toDateTimeLocalValue(value: string | null): string {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  const hours = `${date.getHours()}`.padStart(2, '0');
  const minutes = `${date.getMinutes()}`.padStart(2, '0');
  return `${year}-${month}-${day}T${hours}:${minutes}`;
}

export function fromDateTimeLocalValue(value: string, timeZone: string): string | null {
  const trimmedValue = value.trim();

  if (!trimmedValue) {
    return null;
  }

  const date = new Date(trimmedValue);

  if (Number.isNaN(date.getTime())) {
    return null;
  }

  if (!timeZone) {
    return date.toISOString();
  }

  return date.toISOString();
}
