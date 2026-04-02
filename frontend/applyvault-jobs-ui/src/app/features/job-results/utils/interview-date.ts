const DATE_ONLY_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

export function normalizeInterviewDate(value: string | null | undefined): string | null {
  const normalized = value?.trim() ?? '';
  return normalized.length > 0 ? normalized : null;
}

export function isValidInterviewDate(value: string | null): boolean {
  if (value === null) {
    return true;
  }

  if (!DATE_ONLY_PATTERN.test(value)) {
    return false;
  }

  const [year, month, day] = value.split('-').map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));

  return (
    parsed.getUTCFullYear() === year &&
    parsed.getUTCMonth() === month - 1 &&
    parsed.getUTCDate() === day
  );
}

const interviewDateFormatter = new Intl.DateTimeFormat(undefined, {
  month: 'short',
  day: 'numeric',
  year: 'numeric',
  timeZone: 'UTC'
});

export function formatInterviewDate(value: string | null): string {
  if (!value || !isValidInterviewDate(value)) {
    return 'Not scheduled';
  }

  const [year, month, day] = value.split('-').map(Number);
  return interviewDateFormatter.format(new Date(Date.UTC(year, month - 1, day)));
}
