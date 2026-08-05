import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { API_CONFIG } from '../../../core/config/api.config';
import { TEST_API_BASE_URL } from '../../../../testing/api-fixtures';
import { InterviewPrepPageComponent } from './interview-prep-page.component';

describe('InterviewPrepPageComponent', () => {
  let fixture: ComponentFixture<InterviewPrepPageComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InterviewPrepPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: API_CONFIG, useValue: { baseUrl: TEST_API_BASE_URL } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(InterviewPrepPageComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('shows CV gate when structured CV is missing', () => {
    const structured = httpMock.expectOne(
      `${TEST_API_BASE_URL}/cv-documents/current/structured`
    );
    structured.flush({ documentId: 'd1', structuredImportedAt: null, sections: [] });

    const jobs = httpMock.expectOne(`${TEST_API_BASE_URL}/scrape-results`);
    jobs.flush([]);

    const history = httpMock.expectOne(`${TEST_API_BASE_URL}/interview-prep/sessions`);
    history.flush({ items: [] });

    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Structured CV required');
  });
});
