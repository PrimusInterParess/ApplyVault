import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { API_CONFIG } from '../../../core/config/api.config';
import { createSavedJobResult, TEST_API_BASE_URL } from '../../../../testing/api-fixtures';
import { InterviewPrepFacade } from './interview-prep.facade';

describe('InterviewPrepFacade', () => {
  let facade: InterviewPrepFacade;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        InterviewPrepFacade,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: TEST_API_BASE_URL } }
      ]
    });

    facade = TestBed.inject(InterviewPrepFacade);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('marks CV gate missing on structured 404', () => {
    facade.loadCvGate();

    const request = httpMock.expectOne(`${TEST_API_BASE_URL}/cv-documents/current/structured`);
    request.flush('Not found', { status: 404, statusText: 'Not Found' });

    expect(facade.cvGateStatus()).toBe('missing');
  });

  it('marks CV gate missing when structured sections are empty', () => {
    facade.loadCvGate();

    httpMock
      .expectOne(`${TEST_API_BASE_URL}/cv-documents/current/structured`)
      .flush({ documentId: 'doc-1', structuredImportedAt: null, sections: [] });

    expect(facade.cvGateStatus()).toBe('missing');
  });

  it('maps jobId query to scrapeResultId on turns', () => {
    facade.loadCvGate();
    httpMock
      .expectOne(`${TEST_API_BASE_URL}/cv-documents/current/structured`)
      .flush({
        documentId: 'doc-1',
        structuredImportedAt: null,
        sections: [{ id: 's1', heading: 'Summary', sectionType: 'Summary', sortOrder: 0, entries: [] }]
      });

    expect(facade.cvGateStatus()).toBe('ready');

    facade.setMode('screening');
    facade.setLanguageMix('da');
    facade.setScrapeResultIdFromJobId('11111111-1111-1111-1111-111111111111');
    facade.startSession();

    const turn = httpMock.expectOne(`${TEST_API_BASE_URL}/interview-prep/turns`);
    expect(turn.request.body).toEqual({
      mode: 'screening',
      languageMix: 'da',
      userMessage: "Let's start.",
      scrapeResultId: '11111111-1111-1111-1111-111111111111',
      priorTurns: []
    });

    turn.flush({
      phase: 'interview',
      inference: {
        role: 'Operations coordinator',
        seniority: 'mid',
        interviewStyle: 'screening',
        isTechnicalContext: false
      },
      coachMessage: 'Why are you interested in this role?',
      scorecard: {
        overall: 72,
        summary: 'Solid motivation.',
        dimensions: [
          { id: 'clarity', score: 70, note: 'Clear opening.' },
          { id: 'evidence', score: 65, note: 'Add a concrete example.' },
          { id: 'structure', score: 75, note: 'Good outline.' },
          { id: 'role_fit', score: 80, note: 'Aligned with ops.' },
          { id: 'language', score: 70, note: 'Natural Danish.' }
        ]
      },
      followUps: ['Ask about team size'],
      debriefBullets: []
    });

    expect(facade.sessionStarted()).toBeTrue();
    expect(facade.messages().length).toBe(2);
    expect(facade.messages()[1].role).toBe('coach');
    expect(facade.inference()?.role).toBe('Operations coordinator');
    expect(facade.scorecard()?.overall).toBe(72);
    expect(facade.priorTurns().length).toBe(2);
  });

  it('resolves owned job labels after loadOwnedJobs and clears on general prep', () => {
    const jobId = '11111111-1111-1111-1111-111111111111';
    facade.setScrapeResultIdFromJobId(jobId);
    facade.loadOwnedJobs();

    httpMock.expectOne(`${TEST_API_BASE_URL}/scrape-results`).flush([
      createSavedJobResult({
        id: jobId,
        payload: {
          ...createSavedJobResult().payload,
          jobDetails: {
            ...createSavedJobResult().payload.jobDetails,
            jobTitle: 'Pediatric nurse',
            companyName: 'City Clinic'
          }
        }
      })
    ]);

    expect(facade.jobLinkStatus()).toBe('ready');
    expect(facade.scrapeResultId()).toBe(jobId);
    expect(facade.linkedJob()?.label).toBe('City Clinic · Pediatric nurse');
    expect(facade.jobOptions().length).toBe(1);

    facade.clearJobLink();
    expect(facade.scrapeResultId()).toBeNull();
    expect(facade.linkedJob()).toBeNull();
  });

  it('marks invalid deep-link jobId and clears scrapeResultId after jobs load', () => {
    facade.setScrapeResultIdFromJobId('99999999-9999-9999-9999-999999999999');
    facade.loadOwnedJobs();

    httpMock.expectOne(`${TEST_API_BASE_URL}/scrape-results`).flush([createSavedJobResult()]);

    expect(facade.jobLinkStatus()).toBe('invalid');
    expect(facade.scrapeResultId()).toBeNull();
    expect(facade.jobLinkError()).toContain('could not be found');
  });

  it('surfaces a clear AI-off / validation message on 400', () => {
    facade.loadCvGate();
    httpMock
      .expectOne(`${TEST_API_BASE_URL}/cv-documents/current/structured`)
      .flush({ documentId: 'doc-1', structuredImportedAt: null, sections: [{ id: 's1' }] });

    facade.startSession();
    httpMock.expectOne(`${TEST_API_BASE_URL}/interview-prep/turns`).flush(
      'Interview Prep AI is unavailable because Google AI is disabled.',
      { status: 400, statusText: 'Bad Request' }
    );

    expect(facade.turnError()).toContain('unavailable');
    expect(facade.messages().length).toBe(0);
  });
});
