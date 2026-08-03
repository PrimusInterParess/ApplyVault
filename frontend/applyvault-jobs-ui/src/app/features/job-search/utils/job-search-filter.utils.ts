export const JOB_SEARCH_PAGE_SIZE_OPTIONS = [5, 10, 20] as const;
export const JOB_SEARCH_DEFAULT_PAGE_SIZE = 10;

export type JobSearchPageSize = (typeof JOB_SEARCH_PAGE_SIZE_OPTIONS)[number];

export type EuresSortSearch = 'MOST_RECENT' | 'BEST_MATCH';
export type EuresPublicationPeriod = 'LAST_WEEK' | 'LAST_MONTH';
export type EuresScheduleCode = 'fulltime' | 'parttime';

export type EuresPublishedUrlValue = 'week' | 'month';
export type EuresScheduleUrlValue = 'fulltime' | 'parttime';

export const EURES_DEFAULT_SORT_SEARCH: EuresSortSearch = 'MOST_RECENT';

const PUBLICATION_URL_TO_API: Record<EuresPublishedUrlValue, EuresPublicationPeriod> = {
  week: 'LAST_WEEK',
  month: 'LAST_MONTH'
};

const PUBLICATION_API_TO_URL: Record<EuresPublicationPeriod, EuresPublishedUrlValue> = {
  LAST_WEEK: 'week',
  LAST_MONTH: 'month'
};

export function isJobSearchPageSize(value: number): value is JobSearchPageSize {
  return (JOB_SEARCH_PAGE_SIZE_OPTIONS as readonly number[]).includes(value);
}

export function parseJobSearchPageSize(raw: string | null): JobSearchPageSize | null {
  if (!raw?.trim()) {
    return null;
  }

  const parsed = Number(raw.trim());

  if (!Number.isInteger(parsed) || !isJobSearchPageSize(parsed)) {
    return null;
  }

  return parsed;
}

export function parseEuresSortSearch(raw: string | null): EuresSortSearch | null {
  if (!raw?.trim()) {
    return null;
  }

  const normalized = raw.trim().toUpperCase();

  if (normalized === 'MOST_RECENT' || normalized === 'BEST_MATCH') {
    return normalized;
  }

  return null;
}

export function parseEuresPublicationPeriod(raw: string | null): EuresPublicationPeriod | null {
  if (!raw?.trim()) {
    return null;
  }

  const normalized = raw.trim().toLowerCase();

  if (normalized === 'week' || normalized === 'month') {
    return PUBLICATION_URL_TO_API[normalized];
  }

  return null;
}

export function parseEuresScheduleCode(raw: string | null): EuresScheduleCode | null {
  if (!raw?.trim()) {
    return null;
  }

  const normalized = raw.trim().toLowerCase();

  if (normalized === 'fulltime' || normalized === 'parttime') {
    return normalized;
  }

  return null;
}

export function publicationPeriodToUrl(
  period: EuresPublicationPeriod | null
): EuresPublishedUrlValue | null {
  return period ? PUBLICATION_API_TO_URL[period] : null;
}

export function scheduleCodeToUrl(code: EuresScheduleCode | null): EuresScheduleUrlValue | null {
  return code;
}

export function resolveEuresSortSearch(
  sortSearch: EuresSortSearch,
  keywordCount: number
): EuresSortSearch {
  return keywordCount >= 2 ? 'BEST_MATCH' : sortSearch;
}
