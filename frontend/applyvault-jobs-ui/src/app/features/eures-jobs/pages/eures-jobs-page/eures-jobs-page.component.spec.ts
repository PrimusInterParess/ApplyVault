import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { API_CONFIG } from '../../../../core/config/api.config';
import { AuthService } from '../../../../core/auth/auth.service';
import { authInterceptor } from '../../../../core/auth/auth.interceptor';
import {
  createEuresJobDetail,
  createEuresListing,
  createEuresSearchResponse,
  createSavedJobResult,
  TEST_API_BASE_URL
} from '../../../../../testing/api-fixtures';
import { createAuthServiceMock } from '../../../../../testing/auth-test-utils';
import { EuresJobsPageComponent } from './eures-jobs-page.component';

describe('EuresJobsPageComponent', () => {
  let fixture: ComponentFixture<EuresJobsPageComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EuresJobsPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: TEST_API_BASE_URL } },
        {
          provide: AuthService,
          useValue: createAuthServiceMock({ authenticated: true })
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(EuresJobsPageComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function flushBootstrapRequests(searchResponse = createEuresSearchResponse()) {
    httpMock.expectOne(`${TEST_API_BASE_URL}/scrape-results`).flush([createSavedJobResult()]);
    httpMock.expectOne(`${TEST_API_BASE_URL}/calendar-connections`).flush([]);

    const searchRequest = httpMock.expectOne(`${TEST_API_BASE_URL}/eures/jobs/search`);
    expect(searchRequest.request.method).toBe('POST');
    searchRequest.flush(searchResponse);

    httpMock
      .expectOne(`${TEST_API_BASE_URL}/eures/jobs/eures-1?requestLanguage=en`)
      .flush(createEuresJobDetail());
  }

  function flushFollowUpSearch(searchResponse = createEuresSearchResponse()) {
    httpMock.expectOne(`${TEST_API_BASE_URL}/eures/jobs/search`).flush(searchResponse);
    httpMock
      .match(`${TEST_API_BASE_URL}/scrape-results`)
      .forEach((request) => request.flush([createSavedJobResult()]));
    httpMock
      .match(`${TEST_API_BASE_URL}/eures/jobs/eures-1?requestLanguage=en`)
      .forEach((request) => request.flush(createEuresJobDetail()));
  }

  it('submits an EURES search and renders result cards', () => {
    fixture.detectChanges();
    flushBootstrapRequests();
    fixture.detectChanges();

    const cards = fixture.nativeElement.querySelectorAll('.job-card');

    expect(cards.length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Backend Developer');
    expect(fixture.nativeElement.textContent).toContain('Nordic Tech');
  });

  it('runs a new search when keywords are submitted', () => {
    fixture.detectChanges();
    flushBootstrapRequests();
    fixture.detectChanges();

    const keywordInput = fixture.nativeElement.querySelector(
      'input[type="search"]'
    ) as HTMLInputElement;
    keywordInput.value = 'angular';
    keywordInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const searchButton = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>
    ).find((button) => button.textContent?.includes('Search EURES')) as HTMLButtonElement;
    searchButton.click();

    const searchRequest = httpMock.expectOne(`${TEST_API_BASE_URL}/eures/jobs/search`);
    expect(searchRequest.request.body.keywords).toContain('angular');

    flushFollowUpSearch(
      createEuresSearchResponse({
        jobs: [
          createEuresListing({ id: 'eures-1' }),
          createEuresListing({ id: 'eures-2', title: 'Frontend Developer' })
        ],
        totalResults: 2
      })
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.job-card').length).toBe(2);
  });
});
