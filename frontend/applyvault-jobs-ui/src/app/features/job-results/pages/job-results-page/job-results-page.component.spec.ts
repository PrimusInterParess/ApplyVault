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
  createSavedJobResult,
  TEST_API_BASE_URL
} from '../../../../../testing/api-fixtures';
import { createAuthServiceMock } from '../../../../../testing/auth-test-utils';
import { JobResultsPageComponent } from './job-results-page.component';

describe('JobResultsPageComponent', () => {
  let fixture: ComponentFixture<JobResultsPageComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [JobResultsPageComponent],
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

    fixture = TestBed.createComponent(JobResultsPageComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function flushInitialRequests(results: readonly ReturnType<typeof createSavedJobResult>[] = []) {
    const scrapeRequest = httpMock.expectOne(`${TEST_API_BASE_URL}/scrape-results`);
    scrapeRequest.flush(results);

    const calendarRequest = httpMock.expectOne(`${TEST_API_BASE_URL}/calendar-connections`);
    calendarRequest.flush([]);
  }

  it('renders the empty state when there are no saved jobs', () => {
    fixture.detectChanges();
    flushInitialRequests([]);

    const emptyHeading = fixture.nativeElement.querySelector('.jobs-page__empty h2');

    expect(emptyHeading?.textContent).toContain('No saved jobs yet');
  });

  it('renders saved job cards when the API returns results', () => {
    fixture.detectChanges();
    flushInitialRequests([
      createSavedJobResult(),
      createSavedJobResult({
        id: 'job-2',
        payload: {
          ...createSavedJobResult().payload,
          jobDetails: {
            ...createSavedJobResult().payload.jobDetails,
            jobTitle: 'Platform Engineer',
            companyName: 'CloudWorks'
          }
        }
      })
    ]);
    fixture.detectChanges();

    const cards = fixture.nativeElement.querySelectorAll('.job-card');

    expect(cards.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Software Engineer');
    expect(fixture.nativeElement.textContent).toContain('Platform Engineer');
  });
});
