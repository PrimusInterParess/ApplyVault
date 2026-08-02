import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { API_CONFIG } from '../../../../core/config/api.config';
import { createSavedJobResult, TEST_API_BASE_URL } from '../../../../../testing/api-fixtures';
import { InterviewPrepFacade } from '../../data-access/interview-prep.facade';
import { InterviewPrepPageComponent } from './interview-prep-page.component';

describe('InterviewPrepPageComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InterviewPrepPageComponent],
      providers: [
        InterviewPrepFacade,
        provideRouter([{ path: 'interview-prep', component: InterviewPrepPageComponent }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: TEST_API_BASE_URL } }
      ]
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function flushCvReady(): void {
    httpMock.expectOne(`${TEST_API_BASE_URL}/cv-documents/current/structured`).flush({
      documentId: 'doc-1',
      structuredImportedAt: null,
      sections: [{ id: 's1', heading: 'Summary', sectionType: 'Summary', sortOrder: 0, entries: [] }]
    });
  }

  it('maps ?jobId= deep-link to the job picker selection label', async () => {
    const jobId = '11111111-1111-1111-1111-111111111111';
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(`/interview-prep?jobId=${jobId}`, InterviewPrepPageComponent);

    flushCvReady();
    httpMock.expectOne(`${TEST_API_BASE_URL}/scrape-results`).flush([
      createSavedJobResult({
        id: jobId,
        payload: {
          ...createSavedJobResult().payload,
          jobDetails: {
            ...createSavedJobResult().payload.jobDetails,
            jobTitle: 'Care coordinator',
            companyName: 'North Health'
          }
        }
      })
    ]);
    harness.detectChanges();

    const facade = TestBed.inject(InterviewPrepFacade);
    expect(facade.scrapeResultId()).toBe(jobId);
    expect(facade.linkedJob()?.company).toBe('North Health');
    expect(facade.linkedJob()?.title).toBe('Care coordinator');

    const select = harness.routeNativeElement?.querySelector(
      '.interview-prep__select'
    ) as HTMLSelectElement | null;
    expect(select?.value).toBe(jobId);
    expect(harness.routeNativeElement?.textContent).toContain('North Health');
    expect(harness.routeNativeElement?.textContent).toContain('Care coordinator');
  });

  it('updates the URL when the user picks a saved job', async () => {
    const jobId = '22222222-2222-2222-2222-222222222222';
    const harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl('/interview-prep', InterviewPrepPageComponent);
    const router = TestBed.inject(Router);

    flushCvReady();
    httpMock.expectOne(`${TEST_API_BASE_URL}/scrape-results`).flush([
      createSavedJobResult({
        id: jobId,
        payload: {
          ...createSavedJobResult().payload,
          jobDetails: {
            ...createSavedJobResult().payload.jobDetails,
            jobTitle: 'Analyst',
            companyName: 'Data Co'
          }
        }
      })
    ]);
    harness.detectChanges();

    page.onJobSelect(jobId);
    harness.detectChanges();
    await Promise.resolve();

    expect(TestBed.inject(InterviewPrepFacade).scrapeResultId()).toBe(jobId);
    expect(router.url).toContain(`jobId=${jobId}`);
  });

  it('clears jobId from the URL for general prep', async () => {
    const jobId = '33333333-3333-3333-3333-333333333333';
    const harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl(
      `/interview-prep?jobId=${jobId}`,
      InterviewPrepPageComponent
    );
    const router = TestBed.inject(Router);

    flushCvReady();
    httpMock.expectOne(`${TEST_API_BASE_URL}/scrape-results`).flush([createSavedJobResult({ id: jobId })]);
    harness.detectChanges();

    page.onJobSelect('');
    harness.detectChanges();
    await Promise.resolve();

    expect(TestBed.inject(InterviewPrepFacade).scrapeResultId()).toBeNull();
    expect(router.url).not.toContain('jobId=');
  });
});
