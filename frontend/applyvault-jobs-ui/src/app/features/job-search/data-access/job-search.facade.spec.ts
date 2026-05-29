import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { convertToParamMap } from '@angular/router';

import { API_CONFIG } from '../../../core/config/api.config';
import { AuthService } from '../../../core/auth/auth.service';
import { createAuthServiceMock } from '../../../../testing/auth-test-utils';
import { TEST_API_BASE_URL } from '../../../../testing/api-fixtures';
import { EuresJobsFacade } from './eures-jobs.facade';
import { JobnetJobsFacade } from './jobnet-jobs.facade';
import { JobSearchFacade } from './job-search.facade';

describe('JobSearchFacade', () => {
  let facade: JobSearchFacade;
  let euresFacade: EuresJobsFacade;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        JobSearchFacade,
        EuresJobsFacade,
        JobnetJobsFacade,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: TEST_API_BASE_URL } },
        {
          provide: AuthService,
          useValue: createAuthServiceMock({ authenticated: false })
        }
      ]
    });

    facade = TestBed.inject(JobSearchFacade);
    euresFacade = TestBed.inject(EuresJobsFacade);
  });

  it('keeps keywords in sync when toggling chips on the active source', () => {
    facade.toggleKeyword('Angular');

    expect(euresFacade.keywords()).toContain('Angular');
  });

  it('reads EURES country from the URL', () => {
    const params = convertToParamMap({
      source: 'eures',
      keywords: 'developer',
      country: 'dk'
    });

    facade.initFromQueryParams(params);

    expect(euresFacade.locationCode()).toBe('dk');
  });

  it('builds EURES URL params', () => {
    euresFacade.locationCode.set('dk');
    euresFacade.keywords.set(['Angular']);
    facade.source.set('eures');

    expect(facade.buildQueryParamState()).toEqual({
      source: null,
      keywords: 'Angular',
      country: 'dk',
      location: null,
      selected: null
    });
  });

  it('builds Jobnet URL params', () => {
    const jobnetFacade = TestBed.inject(JobnetJobsFacade);
    jobnetFacade.keywords.set(['developer']);
    facade.source.set('jobnet');

    expect(facade.buildQueryParamState()).toEqual({
      source: 'jobnet',
      keywords: 'developer',
      country: null,
      location: null,
      selected: null
    });
  });

  it('updates idle copy when switching providers', () => {
    expect(facade.resultsSummary()).toContain('EURES');

    facade.setSource('jobnet');

    expect(facade.resultsSummary()).toContain('Work in Denmark');
    expect(facade.emptyStateIntro()).toContain('Work in Denmark');
  });
});
