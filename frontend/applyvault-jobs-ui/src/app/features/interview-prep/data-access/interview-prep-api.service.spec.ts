import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { API_CONFIG } from '../../../core/config/api.config';
import { TEST_API_BASE_URL } from '../../../../testing/api-fixtures';
import {
  InterviewPrepApiService,
  normalizeTurnResponse
} from './interview-prep-api.service';
import { InterviewPrepTurnResponse } from '../models/interview-prep.model';

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

  it('POSTs a turn to /interview-prep/turns and normalizes arrays', () => {
    let result: InterviewPrepTurnResponse | undefined;

    api
      .createTurn({
        mode: 'behavioral',
        languageMix: 'en',
        userMessage: "Let's start.",
        priorTurns: []
      })
      .subscribe((response) => {
        result = response;
      });

    const request = httpMock.expectOne(`${TEST_API_BASE_URL}/interview-prep/turns`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.mode).toBe('behavioral');
    expect(request.request.body.languageMix).toBe('en');

    request.flush({
      phase: 'interview',
      inference: {
        role: 'Pediatric nurse',
        seniority: 'mid',
        interviewStyle: 'competency_behavioral',
        isTechnicalContext: false
      },
      coachMessage: 'Tell me about a time you handled a difficult family conversation.',
      scorecard: null,
      followUps: null,
      debriefBullets: null
    });

    expect(result?.coachMessage).toContain('family conversation');
    expect(result?.followUps).toEqual([]);
    expect(result?.debriefBullets).toEqual([]);
    expect(result?.inference.role).toBe('Pediatric nurse');
  });

  it('normalizeTurnResponse fills missing inference safely', () => {
    const normalized = normalizeTurnResponse({
      phase: '',
      inference: {
        role: '',
        seniority: '',
        interviewStyle: '',
        isTechnicalContext: false
      },
      coachMessage: '  Hello  ',
      scorecard: null,
      followUps: ['  Keep STAR structure  ', ''],
      debriefBullets: []
    });

    expect(normalized.phase).toBe('interview');
    expect(normalized.coachMessage).toBe('Hello');
    expect(normalized.inference.role).toBe('Unknown role');
    expect(normalized.followUps).toEqual(['Keep STAR structure']);
  });
});
