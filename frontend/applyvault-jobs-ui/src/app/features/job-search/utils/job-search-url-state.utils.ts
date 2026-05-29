import { ParamMap } from '@angular/router';

import { JobSearchUrlQueryParams } from '../models/external-job.model';
import { JobSearchSource, normalizeJobSearchSource } from '../models/job-source.model';
import { isKnownEuresLocationCode, normalizeEuresLocationCode } from '../models/eures-location-options';
import { normalizeEuresKeywords } from './eures-keyword.utils';

export const JOB_SEARCH_URL_QUERY_KEYS = [
  'source',
  'keywords',
  'country',
  'location',
  'selected'
] as const;

export function buildJobSearchUrlQueryParams(state: {
  source: JobSearchSource;
  keywords: readonly string[];
  country: string | null;
  selectedJobId: string | null;
}): JobSearchUrlQueryParams {
  return {
    source: state.source === 'eures' ? null : state.source,
    keywords: state.keywords.length > 0 ? state.keywords.join(',') : null,
    country: state.source === 'eures' ? state.country : null,
    location: null,
    selected: state.selectedJobId
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

export function buildJobSearchQueryKeyFromParams(params: ParamMap): string {
  const source = normalizeJobSearchSourceFromParams(params);

  return JSON.stringify(
    buildJobSearchUrlQueryParams({
      source,
      keywords: readJobSearchKeywordsFromQueryParams(params),
      country: source === 'eures' ? readEuresCountryFromQueryParams(params) : null,
      selectedJobId: params.get('selected')
    })
  );
}
