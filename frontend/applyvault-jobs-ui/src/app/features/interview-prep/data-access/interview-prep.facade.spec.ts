import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { JobResultsApiService } from '../../job-results/data-access/job-results-api.service';
import { PreparationPack } from '../models/interview-prep.model';
import { discoveryPack, preparationPack } from '../testing/preparation-fixtures';
import { InterviewPrepApiService } from './interview-prep-api.service';
import { InterviewPrepFacade } from './interview-prep.facade';

describe('InterviewPrepFacade', () => {
  let facade: InterviewPrepFacade;
  let api: jasmine.SpyObj<InterviewPrepApiService>;
  beforeEach(() => {
    api = jasmine.createSpyObj<InterviewPrepApiService>('api', ['context', 'list', 'generate', 'regenerate', 'saveProgress', 'delete', 'prepareDocument', 'saveAnswers', 'writeDocument', 'help']);
    api.context.and.returnValue(of({ hasStructuredCv: true, projects: [] }));
    api.list.and.returnValue(of([]));
    const jobs = jasmine.createSpyObj<JobResultsApiService>('jobs', ['getAll']);
    jobs.getAll.and.returnValue(of([]));
    TestBed.configureTestingModule({ providers: [InterviewPrepFacade,
      { provide: InterviewPrepApiService, useValue: api }, { provide: JobResultsApiService, useValue: jobs }] });
    facade = TestBed.inject(InterviewPrepFacade);
  });

  it('requires a target for general preparation and uses the selected pack binding', () => {
    facade.load();
    expect(facade.canGenerate()).toBeFalse();
    facade.patchInput({ targetRole: 'Designer', targetLevel: 'Junior' });
    expect(facade.canGenerate()).toBeTrue();
    facade.select(preparationPack());
    facade.setJob('another-job');
    expect(facade.selected()).toBeNull();
    expect(facade.input().scrapeResultId).toBe('another-job');
  });

  it('keeps saved documents and unsaved progress when regeneration fails', () => {
    facade.load(); const pack = preparationPack(); facade.select(pack);
    facade.setReviewLog('My observed weakness');
    api.regenerate.and.returnValue(throwError(() => new HttpErrorResponse({ status: 503, error: { message: 'Provider unavailable' } })));
    facade.generate();
    expect(facade.selected()).toBe(pack);
    expect(facade.progress().reviewLog).toBe('My observed weakness');
    expect(facade.error()).toBe('Provider unavailable');
    expect(facade.busy()).toBeNull();
  });

  it('prevents duplicate generation while a request is in progress', () => {
    facade.load(); facade.patchInput(preparationPack().input);
    const result = new Subject<PreparationPack>(); api.generate.and.returnValue(result);
    facade.generate(); facade.generate();
    expect(api.generate).toHaveBeenCalledTimes(1);
    expect(facade.busy()).toBe('generating');
    result.next(preparationPack()); result.complete();
    expect(facade.selected()?.content?.documents.length).toBe(1);
    expect(facade.busy()).toBeNull();
  });

  it('saves independent readiness and the review log with the current revision', () => {
    facade.load(); const pack = preparationPack(); facade.select(pack);
    facade.toggleReadiness('demonstratedDocumentIds', 'foundations', true);
    facade.setReviewLog('Need to adapt the example.');
    expect(facade.progressDirty()).toBeTrue();
    const progress = facade.progress();
    api.saveProgress.and.returnValue(of({ ...pack, progress, updatedAt: '2026-11-26T11:00:00Z' }));
    facade.saveProgress();
    expect(api.saveProgress).toHaveBeenCalledWith(pack, progress);
    expect(facade.progressDirty()).toBeFalse();
    expect(facade.progress().readDocumentIds).toEqual([]);
    expect(facade.progress().demonstratedDocumentIds).toEqual(['foundations']);
  });
  it('keeps guidance separate from the user draft and saved answers', () => {
    facade.load(); const pack = discoveryPack(); facade.select(pack);
    facade.updateAnswer('foundations', 'foundations-needs', { status: 'unknown', text: 'I cannot explain it yet.' });
    const answers = facade.answers('foundations');
    api.help.and.returnValue(of({ markdown: 'Hypothetical guidance, not your history.', isDemo: false }));
    facade.askForHelp('foundations', 'foundations-needs', 'Show me how to start.');
    expect(facade.answers('foundations')).toEqual(answers);
    expect(facade.selected()?.discovery?.answers).toEqual([]);
    expect(api.saveAnswers).not.toHaveBeenCalled();
    expect(facade.helpResult()?.response.markdown).toContain('Hypothetical');
    expect(facade.unsaved()).toBeTrue();
  });

  it('saves explicit unknown and skip statuses before writing just one document', () => {
    facade.load(); const pack = discoveryPack(); facade.select(pack);
    expect(facade.canWrite('foundations')).toBeFalse();
    facade.updateAnswer('foundations', 'foundations-needs', { status: 'unknown', text: '' });
    facade.updateAnswer('foundations', 'foundations-contribution', { status: 'skipped', text: '' });
    expect(facade.canWrite('foundations')).toBeTrue();
    const answers = facade.answers('foundations');
    const saved: PreparationPack = { ...pack, updatedAt: '2026-11-26T11:00:00Z', discovery: {
      ...pack.discovery!, answers: answers.map(a => ({ ...a, documentId: 'foundations' })), contextRevision: 1
    } };
    api.saveAnswers.and.returnValue(of(saved));
    api.writeDocument.and.returnValue(of({ ...saved, content: preparationPack().content }));
    facade.setReviewLog('Preserve my unsaved learning observation.');
    facade.writeDocument('foundations');
    expect(api.saveAnswers).toHaveBeenCalledWith(pack, 'foundations', answers);
    expect(api.writeDocument).toHaveBeenCalledWith(saved, 'foundations');
    expect(facade.progress().reviewLog).toBe('Preserve my unsaved learning observation.');
    expect(facade.discoveryDirty()).toBeFalse();
  });

  it('preserves saved context and existing content if writing fails after answers were saved', () => {
    facade.load(); const draft = discoveryPack();
    const pack: PreparationPack = { ...draft, content: preparationPack().content };
    facade.select(pack);
    facade.updateAnswer('foundations', 'foundations-needs', { status: 'unknown' });
    facade.updateAnswer('foundations', 'foundations-contribution', { status: 'unknown' });
    const saved: PreparationPack = { ...pack, updatedAt: '2026-11-26T11:00:00Z', discovery: {
      ...pack.discovery!, answers: facade.answers('foundations').map(a => ({ ...a, documentId: 'foundations' })), contextRevision: 1
    } };
    api.saveAnswers.and.returnValue(of(saved));
    api.writeDocument.and.returnValue(throwError(() => new HttpErrorResponse({ status: 503 })));
    facade.writeDocument('foundations');
    expect(facade.selected()?.content).toBe(pack.content);
    expect(facade.selected()?.discovery?.answers.length).toBe(2);
    expect(facade.discoveryDirty()).toBeFalse();
    expect(facade.error()).toContain('preserved');
  });

  it('refreshes later questions from shared context without replacing answers already in progress', () => {
    facade.load(); const pack = discoveryPack();
    facade.select({ ...pack, discovery: { ...pack.discovery!, contextRevision: 1 } });
    expect(facade.questionsNeedRefresh('foundations')).toBeTrue();
    facade.updateAnswer('foundations', 'foundations-needs', { status: 'answered', text: 'My own draft.' });
    expect(facade.questionsNeedRefresh('foundations')).toBeFalse();
  });

  it('allows the reported 40-day preparation window', () => {
    facade.load();
    facade.patchInput({ targetRole: 'Full-stack developer', targetLevel: 'Senior', preparationDays: 40 });
    expect(facade.canGenerate()).toBeTrue();
  });

});
