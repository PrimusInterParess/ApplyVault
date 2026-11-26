import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from '../../../core/config/api.config';
import { TEST_API_BASE_URL } from '../../../../testing/api-fixtures';
import { InterviewPrepApiService } from './interview-prep-api.service';
import { preparationPack } from '../testing/preparation-fixtures';

describe('InterviewPrepApiService', () => {
  let api: InterviewPrepApiService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: API_CONFIG, useValue: { baseUrl: TEST_API_BASE_URL } }] });
    api = TestBed.inject(InterviewPrepApiService); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('sends personalization inputs to document generation', () => {
    const pack = preparationPack();
    api.generate(pack.input).subscribe();
    const request = http.expectOne(`${TEST_API_BASE_URL}/interview-prep/packs`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.targetRole).toBe('Product designer');
    expect(request.request.body.scrapeResultId).toBeNull();
    request.flush(pack);
  });

  it('includes the revision when replacing documents or saving progress', () => {
    const pack = preparationPack();
    api.regenerate(pack, pack.input).subscribe();
    const regenerate = http.expectOne(`${TEST_API_BASE_URL}/interview-prep/packs/${pack.id}/regenerate`);
    expect(regenerate.request.body).toEqual({ expectedUpdatedAt: pack.updatedAt, input: pack.input });
    regenerate.flush(pack);
    api.saveProgress(pack, pack.progress).subscribe();
    const progress = http.expectOne(`${TEST_API_BASE_URL}/interview-prep/packs/${pack.id}/progress`);
    expect(progress.request.method).toBe('PUT');
    expect(progress.request.body.expectedUpdatedAt).toBe(pack.updatedAt);
    progress.flush(pack);
  });
  it('keeps clarification saves, help, and document generation scoped to the document and revision', () => {
    const pack = preparationPack();
    api.prepareDocument(pack, 'foundations').subscribe();
    const questions = http.expectOne(`${TEST_API_BASE_URL}/interview-prep/packs/pack-1/documents/foundations/discovery`);
    expect(questions.request.body.expectedUpdatedAt).toBe(pack.updatedAt); questions.flush(pack);
    api.saveAnswers(pack, 'foundations', [{ questionId: 'foundations-needs', status: 'unknown', text: '' }]).subscribe();
    const answers = http.expectOne(`${TEST_API_BASE_URL}/interview-prep/packs/pack-1/documents/foundations/answers`);
    expect(answers.request.body.answers[0].status).toBe('unknown'); answers.flush(pack);
    api.help(pack, 'foundations', 'foundations-needs', 'Help me start.', 'My draft').subscribe();
    const help = http.expectOne(`${TEST_API_BASE_URL}/interview-prep/packs/pack-1/documents/foundations/questions/foundations-needs/help`);
    expect(help.request.body.draftAnswer).toBe('My draft'); help.flush({ markdown: 'Guidance', isDemo: false });
    api.writeDocument(pack, 'foundations').subscribe();
    const write = http.expectOne(`${TEST_API_BASE_URL}/interview-prep/packs/pack-1/documents/foundations/generate`);
    expect(write.request.body.expectedUpdatedAt).toBe(pack.updatedAt); write.flush(pack);
  });

});
