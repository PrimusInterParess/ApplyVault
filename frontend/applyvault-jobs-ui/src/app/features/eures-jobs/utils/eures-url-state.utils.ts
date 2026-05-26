import { ParamMap } from '@angular/router';

export const EURES_URL_QUERY_KEYS = ['keywords', 'location', 'page', 'selected', 'pageSize'] as const;

export type EuresUrlQueryKey = (typeof EURES_URL_QUERY_KEYS)[number];

export type EuresUrlQueryParams = Record<EuresUrlQueryKey, string | null>;

export function buildEuresUrlQueryParams(state: {
  keywords: readonly string[];
  locationCode: string;
  page: number;
  selectedJobId: string | null;
  resultsPerPage: number;
  defaultResultsPerPage: number;
}): EuresUrlQueryParams {
  return {
    keywords: state.keywords.length > 0 ? state.keywords.join(',') : null,
    location: state.locationCode || null,
    page: state.page > 1 ? String(state.page) : null,
    selected: state.selectedJobId,
    pageSize:
      state.resultsPerPage !== state.defaultResultsPerPage
        ? String(state.resultsPerPage)
        : null
  };
}

export function euresQueryParamsEqual(
  current: ParamMap,
  next: EuresUrlQueryParams
): boolean {
  for (const key of EURES_URL_QUERY_KEYS) {
    const currentValue = current.get(key);
    const nextValue = next[key];

    if ((currentValue ?? null) !== (nextValue ?? null)) {
      return false;
    }
  }

  return true;
}
