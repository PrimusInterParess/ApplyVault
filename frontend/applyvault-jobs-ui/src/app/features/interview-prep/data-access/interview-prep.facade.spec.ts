import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { API_CONFIG } from '../../../core/config/api.config';
import { TEST_API_BASE_URL } from '../../../../testing/api-fixtures';
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

  it('maps mode to allowed personas', () => {
    facade.setMode('fullLoop');
    expect(facade.availablePersonas()).toEqual(['hiringManager']);
    expect(facade.persona()).toBe('hiringManager');
  });

  it('loads CV gate from structured endpoint', () => {
    facade.loadCvGate();
    const request = httpMock.expectOne(
      `${TEST_API_BASE_URL}/cv-documents/current/structured`
    );
    request.flush({ documentId: 'd1', structuredImportedAt: null, sections: [{ id: 's1' }] });
    expect(facade.cvGateStatus()).toBe('ready');
  });
});
