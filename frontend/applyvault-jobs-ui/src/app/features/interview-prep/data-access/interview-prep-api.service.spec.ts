import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { API_CONFIG } from '../../../core/config/api.config';
import { TEST_API_BASE_URL } from '../../../../testing/api-fixtures';
import { InterviewPrepApiService } from './interview-prep-api.service';

describe('InterviewPrepApiService', () => {
  let api: InterviewPrepApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        InterviewPrepApiService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: TEST_API_BASE_URL } }
      ]
    });
    api = TestBed.inject(InterviewPrepApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('POSTs create session to /interview-prep/sessions', () => {
    api
      .createSession({
        mode: 'behavioralAndCulture',
        persona: 'hiringManager',
        language: 'english',
        market: 'general',
        experienceType: 'guidedCoaching',
        interactionType: 'text'
      })
      .subscribe();

    const request = httpMock.expectOne(`${TEST_API_BASE_URL}/interview-prep/sessions`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.mode).toBe('behavioralAndCulture');
    request.flush({
      id: 'sess-1',
      status: 'created',
      mode: 'behavioralAndCulture',
      persona: 'hiringManager',
      language: 'english',
      market: 'general',
      experienceType: 'guidedCoaching',
      interactionType: 'text',
      scrapeResultId: null,
      jobTitle: null,
      companyName: null,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
      preparedAt: null,
      startedAt: null,
      completedAt: null,
      eTag: 'v1'
    });
  });

  it('sends If-Match on submit turn', () => {
    api
      .submitTurn('sess-1', { clientTurnId: 'client-1', answer: 'Hello' }, 'etag-1')
      .subscribe();

    const request = httpMock.expectOne(`${TEST_API_BASE_URL}/interview-prep/sessions/sess-1/turns`);
    expect(request.request.headers.get('If-Match')).toBe('etag-1');
    request.flush(
      {
        session: { id: 'sess-1', status: 'inProgress', eTag: 'etag-2', turns: [], stages: [] },
        candidateTurn: {
          id: 't1',
          stageId: 's1',
          sequence: 1,
          role: 'candidate',
          text: 'Hello',
          questionSignature: null,
          competencyTag: null,
          language: null,
          clientTurnId: 'client-1',
          createdAt: '2026-01-01T00:00:00Z'
        },
        nextInterviewerTurn: null,
        interviewComplete: false
      },
      { headers: { ETag: 'etag-2' } }
    );
  });
});
