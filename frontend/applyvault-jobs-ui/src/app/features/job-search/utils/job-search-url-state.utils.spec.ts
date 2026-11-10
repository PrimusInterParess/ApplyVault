import { convertToParamMap } from '@angular/router';

import {
  buildJobSearchQueryKeyFromParams,
  buildJobSearchUrlQueryParams,
  readEuresCountryFromQueryParams
} from './job-search-url-state.utils';

describe('job-search-url-state.utils', () => {
  it('writes country for EURES and omits default source', () => {
    const params = buildJobSearchUrlQueryParams({
      source: 'eures',
      keywords: ['Angular'],
      country: 'dk',
      selectedJobId: null
    });

    expect(params).toEqual({
      source: null,
      keywords: 'Angular',
      country: 'dk',
      location: null,
      selected: null,
      sort: null,
      pageSize: null,
      published: null,
      schedule: null,
      place: null
    });
  });

  it('preserves selected job id in URL params', () => {
    const params = buildJobSearchUrlQueryParams({
      source: 'eures',
      keywords: ['React'],
      country: 'de',
      selectedJobId: 'job-1'
    });

    expect(params).toEqual({
      source: null,
      keywords: 'React',
      country: 'de',
      location: null,
      selected: 'job-1',
      sort: null,
      pageSize: null,
      published: null,
      schedule: null,
      place: null
    });
  });

  it('writes a Work in Denmark place and drops an unknown place', () => {
    expect(
      buildJobSearchUrlQueryParams({
        source: 'jobnet',
        keywords: ['developer'],
        country: null,
        selectedJobId: null,
        place: 'aarhus'
      }).place
    ).toBe('aarhus');

    expect(
      buildJobSearchUrlQueryParams({
        source: 'jobnet',
        keywords: ['developer'],
        country: null,
        selectedJobId: null,
        place: 'berlin'
      }).place
    ).toBeNull();
  });
});

describe('job-search query param readers', () => {
  it('reads EURES country from country or legacy location param', () => {
    expect(readEuresCountryFromQueryParams(convertToParamMap({ country: 'de' }))).toBe('de');
    expect(readEuresCountryFromQueryParams(convertToParamMap({ location: 'de' }))).toBe('de');
  });

  it('builds a stable query key from route params', () => {
    const key = buildJobSearchQueryKeyFromParams(
      convertToParamMap({
        source: 'jobnet',
        keywords: 'software, programmør'
      })
    );

    expect(key).toBe(
      JSON.stringify({
        source: 'jobnet',
        keywords: 'software,programmør',
        country: null,
        location: null,
        selected: null,
        sort: null,
        pageSize: null,
        published: null,
        schedule: null,
        place: null
      })
    );
  });
});
