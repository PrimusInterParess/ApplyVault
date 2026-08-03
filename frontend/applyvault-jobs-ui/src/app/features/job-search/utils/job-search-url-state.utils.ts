import { ParamMap } from '@angular/router';

import { JobSearchUrlQueryParams } from '../models/external-job.model';
import { JobSearchSource, normalizeJobSearchSource } from '../models/job-source.model';
import { isKnownEuresLocationCode, normalizeEuresLocationCode } from '../models/eures-location-options';
import { normalizeEuresKeywords } from './eures-keyword.utils';
import {
  EURES_DEFAULT_SORT_SEARCH,
  EuresPublicationPeriod,
  EuresScheduleCode,
  EuresSortSearch,
  JOB_SEARCH_DEFAULT_PAGE_SIZE,
  JobSearchPageSize,
  parseEuresPublicationPeriod,
  parseEuresScheduleCode,
  parseEuresSortSearch,
  parseJobSearchPageSize,
  publicationPeriodToUrl,
  resolveEuresSortSearch,
  scheduleCodeToUrl
} from './job-search-filter.utils';

export const JOB_SEARCH_URL_QUERY_KEYS = [
  'source',
  'keywords',
  'country',
  'location',
  'selected',
  'sort',
  'pageSize',
  'published',
  'schedule'
] as const;

export function buildJobSearchUrlQueryParams(state: {
  source: JobSearchSource;
  keywords: readonly string[];
  country: string | null;
  selectedJobId: string | null;
  sortSearch?: EuresSortSearch;
  pageSize?: JobSearchPageSize;
  publicationPeriod?: EuresPublicationPeriod | null;
  scheduleCode?: EuresScheduleCode | null;
}): JobSearchUrlQueryParams {
  const isEures = state.source === 'eures';
  const pageSize = state.pageSize ?? JOB_SEARCH_DEFAULT_PAGE_SIZE;
  const keywordCount = state.keywords.length;
  const sortSearch = isEures
    ? resolveEuresSortSearch(state.sortSearch ?? EURES_DEFAULT_SORT_SEARCH, keywordCount)
    : null;

  return {
    source: state.source === 'eures' ? null : state.source,
    keywords: state.keywords.length > 0 ? state.keywords.join(',') : null,
    country: isEures ? state.country : null,
    location: null,
    selected: state.selectedJobId,
    sort:
      isEures && sortSearch && sortSearch !== EURES_DEFAULT_SORT_SEARCH ? sortSearch : null,
    pageSize: pageSize === JOB_SEARCH_DEFAULT_PAGE_SIZE ? null : String(pageSize),
    published: isEures ? publicationPeriodToUrl(state.publicationPeriod ?? null) : null,
    schedule: isEures ? scheduleCodeToUrl(state.scheduleCode ?? null) : null
  };
}

export function jobSearchQueryParamsEqual(
  current: ParamMap,
  next: JobSearchUrlQueryParams
): boolean {
  for (const key of JOB_SEARCH_URL_QUERY_KEYS) {
    const currentValue = current.get(key);
    const nextValue = next[key];

    if ((currentValue ?? null) !== (nextValue ?? null)) {
      return false;
    }
  }

  return true;
}

export function readEuresCountryFromQueryParams(params: ParamMap): string | null {
  const countryParam = params.get('country') ?? params.get('location');

  if (!countryParam?.trim()) {
    return null;
  }

  const normalized = normalizeEuresLocationCode(countryParam);
  return isKnownEuresLocationCode(normalized) ? normalized : null;
}

export function normalizeJobSearchSourceFromParams(params: ParamMap): JobSearchSource {
  return normalizeJobSearchSource(params.get('source'));
}

export function readJobSearchKeywordsFromQueryParams(params: ParamMap): readonly string[] {
  const keywordsParam = params.get('keywords');

  if (!keywordsParam?.trim()) {
    return [];
  }

  return normalizeEuresKeywords(keywordsParam.split(/[,;]+/));
}

export function readJobSearchPageSizeFromQueryParams(params: ParamMap): JobSearchPageSize {
  return parseJobSearchPageSize(params.get('pageSize')) ?? JOB_SEARCH_DEFAULT_PAGE_SIZE;
}

export function readEuresSortSearchFromQueryParams(
  params: ParamMap,
  keywordCount: number
): EuresSortSearch {
  const parsed = parseEuresSortSearch(params.get('sort')) ?? EURES_DEFAULT_SORT_SEARCH;
  return resolveEuresSortSearch(parsed, keywordCount);
}

export function readEuresPublicationPeriodFromQueryParams(
  params: ParamMap
): EuresPublicationPeriod | null {
  const raw = params.get('published');

  if (!raw?.trim()) {
    return null;
  }

  return parseEuresPublicationPeriod(raw);
}

export function readEuresScheduleCodeFromQueryParams(params: ParamMap): EuresScheduleCode | null {
  const raw = params.get('schedule');

  if (!raw?.trim()) {
    return null;
  }

  return parseEuresScheduleCode(raw);
}

export function buildJobSearchQueryKeyFromParams(params: ParamMap): string {
  const source = normalizeJobSearchSourceFromParams(params);
  const keywords = readJobSearchKeywordsFromQueryParams(params);

  return JSON.stringify(
    buildJobSearchUrlQueryParams({
      source,
      keywords,
      country: source === 'eures' ? readEuresCountryFromQueryParams(params) : null,
      selectedJobId: params.get('selected'),
      sortSearch:
        source === 'eures'
          ? readEuresSortSearchFromQueryParams(params, keywords.length)
          : undefined,
      pageSize: readJobSearchPageSizeFromQueryParams(params),
      publicationPeriod:
        source === 'eures' ? readEuresPublicationPeriodFromQueryParams(params) : null,
      scheduleCode: source === 'eures' ? readEuresScheduleCodeFromQueryParams(params) : null
    })
  );
}
