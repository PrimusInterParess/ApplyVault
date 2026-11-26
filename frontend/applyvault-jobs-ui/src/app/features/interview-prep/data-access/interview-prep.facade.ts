import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, finalize, forkJoin, Observable, of, switchMap, tap } from 'rxjs';
import { JobResultsApiService } from '../../job-results/data-access/job-results-api.service';
import { SavedJobResult } from '../../job-results/models/job-result.model';
import { InterviewPrepApiService } from './interview-prep-api.service';
import {
  defaultPreparationInput, emptyPreparationProgress, MAX_PREPARATION_DAYS, PreparationAnswerInput, PreparationContext, PreparationDocument, PreparationHelpResponse, PreparationPack,
  PreparationPackInput, PreparationProgress, PreparationReadinessKey
} from '../models/interview-prep.model';

@Injectable()
export class InterviewPrepFacade {
  private readonly api = inject(InterviewPrepApiService);
  private readonly jobsApi = inject(JobResultsApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly context = signal<PreparationContext | null>(null);
  readonly jobs = signal<readonly SavedJobResult[]>([]);
  readonly packs = signal<readonly PreparationPack[]>([]);
  readonly selected = signal<PreparationPack | null>(null);
  readonly input = signal<PreparationPackInput>(defaultPreparationInput());
  readonly progress = signal<PreparationProgress>(emptyPreparationProgress());
  readonly documentId = signal<string | null>(null);
  readonly busy = signal<'loading' | 'generating' | 'regenerating' | 'saving' | 'deleting' | 'questions' | 'answers' | 'writing' | 'helping' | null>(null);
  readonly generationInProgress = computed(() => ['generating', 'regenerating', 'questions', 'writing'].includes(this.busy() ?? ''));
  readonly answerDrafts = signal<Readonly<Record<string, readonly PreparationAnswerInput[]>>>({});
  readonly helpResult = signal<{ documentId: string; questionId: string; response: PreparationHelpResponse } | null>(null);
  readonly error = signal<string | null>(null);
  readonly jobsError = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  readonly document = computed(() => this.selected()?.content?.documents.find(d => d.id === this.documentId()) ?? null);
  readonly totalMinutes = computed(() => this.selected()?.content?.studyPlan.reduce((total, s) => total + s.minutes, 0) ?? 0);
  readonly progressDirty = computed(() => !!this.selected()?.content && JSON.stringify(this.progress()) !== JSON.stringify(this.selected()!.progress));
  readonly discoveryDirty = computed(() => Object.entries(this.answerDrafts()).some(([id, answers]) =>
    JSON.stringify(answers) !== JSON.stringify(this.savedAnswers(id))));
  readonly unsaved = computed(() => this.progressDirty() || this.discoveryDirty());
  readonly generatedCount = computed(() => this.selected()?.content?.documents.filter(d => d.isGenerated !== false).length ?? 0);
  readonly missingProjectIds = computed(() => this.input().projectSummaryIds.filter(id => !this.context()?.projects.some(p => p.id === id)));
  readonly generationIssues = computed(() => {
    const input = this.input();
    const issues: string[] = [];
    if (!this.context()?.hasStructuredCv) issues.push('Build or import your Structured CV first.');
    if (!input.scrapeResultId) {
      if (!input.targetRole.trim()) issues.push('Enter a target role or select a saved job.');
      if (!input.targetLevel.trim()) issues.push('Enter a target level or select a saved job.');
    }
    if (!Number.isInteger(input.preparationDays) || input.preparationDays < 1 || input.preparationDays > MAX_PREPARATION_DAYS)
      issues.push(`Choose 1–${MAX_PREPARATION_DAYS} whole preparation days.`);
    if (!Number.isInteger(input.minutesPerDay) || input.minutesPerDay < 15 || input.minutesPerDay > 240)
      issues.push('Choose 15–240 whole study minutes per day.');
    if (this.missingProjectIds().length) issues.push('Remove unavailable projects from your selection.');
    if (input.projectSummaryIds.length > 10) issues.push('Choose at most 10 project summaries.');
    const formats = [...new Set(input.interviewFormats.map(format => format.trim()).filter(Boolean))];
    if (formats.length > 8) issues.push('Enter at most 8 interview formats.');
    if (formats.some(format => format.length > 120)) issues.push('Keep each interview format within 120 characters.');
    return issues;
  });
  readonly canGenerate = computed(() => !this.busy() && this.generationIssues().length === 0);


  load(jobId?: string | null, packId?: string | null): void {
    if (this.busy()) return;
    this.busy.set('loading');
    this.error.set(null);
    this.jobsError.set(null);
    forkJoin({
      context: this.api.context(), packs: this.api.list(),
      jobs: this.jobsApi.getAll().pipe(catchError(() => {
        this.jobsError.set('Saved jobs could not be loaded. Retry to prepare for a specific posting.');
        return of([] as readonly SavedJobResult[]);
      }))
    }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(null))).subscribe({
      next: ({ context, packs, jobs }) => {
        this.context.set(context); this.packs.set(packs); this.jobs.set(jobs);
        const selected = packId ? packs.find(p => p.id === packId)
          : packs.find(p => p.scrapeResultId === (jobId ?? null));
        if (selected) this.select(selected);
        else {
          this.selected.set(null); this.documentId.set(null); this.progress.set(emptyPreparationProgress());
          this.answerDrafts.set({}); this.helpResult.set(null);
          this.input.set({ ...defaultPreparationInput(), scrapeResultId: jobId ?? null });
          if (packId) this.error.set('This preparation pack is unavailable.');
          if (jobId && !jobs.some(j => j.id === jobId))
            this.error.set('The linked saved job is unavailable. Choose another job or general preparation.');
        }
      },
      error: error => this.error.set(this.readError(error, 'Could not load preparation. Please retry.'))
    });
  }

  select(pack: PreparationPack): void {
    this.selected.set(pack);
    this.answerDrafts.set({}); this.helpResult.set(null);
    // Older briefs and nullable wire fields get complete form defaults.
    this.input.set({
      ...defaultPreparationInput(), ...pack.input,
      targetRole: pack.input.targetRole ?? '', targetLevel: pack.input.targetLevel ?? '',
      hiringMarket: pack.input.hiringMarket ?? '', knownWeaknesses: pack.input.knownWeaknesses ?? '',
      additionalInformation: pack.input.additionalInformation ?? '',
      interviewFormats: pack.input.interviewFormats ?? [], projectSummaryIds: pack.input.projectSummaryIds ?? []
    });
    this.progress.set(pack.progress);
    this.documentId.set(null);
    this.error.set(null); this.notice.set(null);
  }

  setJob(id: string): void {
    const binding = id || null;
    const existing = this.packs().find(p => p.scrapeResultId === binding);
    if (existing) this.select(existing);
    else {
      this.selected.set(null); this.documentId.set(null); this.progress.set(emptyPreparationProgress());
      this.answerDrafts.set({}); this.helpResult.set(null);
      this.patchInput({ scrapeResultId: binding }); this.error.set(null); this.notice.set(null);
    }
  }

  patchInput(patch: Partial<PreparationPackInput>): void {
    this.input.update(input => ({ ...input, ...patch }));
  }

  removeUnavailableProjects(): void {
    this.patchInput({ projectSummaryIds: this.input().projectSummaryIds.filter(id => !this.missingProjectIds().includes(id)) });
  }

  toggleProject(id: string, checked: boolean): void {
    const ids = this.input().projectSummaryIds;
    this.patchInput({ projectSummaryIds: checked ? [...ids, id] : ids.filter(p => p !== id) });
  }

  generate(): void {
    if (!this.canGenerate()) return;
    const pack = this.selected();
    this.mutate(pack ? this.api.regenerate(pack, this.input()) : this.api.generate(this.input()),
      pack ? 'regenerating' : 'generating', true);
  }

  toggleReadiness(key: PreparationReadinessKey, id: string, checked: boolean): void {
    this.progress.update(progress => ({ ...progress, [key]: checked ? [...progress[key], id] : progress[key].filter(x => x !== id) }));
  }

  toggleDay(day: number, checked: boolean): void {
    this.progress.update(p => ({ ...p, completedDays: checked ? [...p.completedDays, day] : p.completedDays.filter(d => d !== day) }));
  }

  setReviewLog(reviewLog: string): void { this.progress.update(p => ({ ...p, reviewLog })); }

  saveProgress(): void {
    const pack = this.selected();
    if (!pack?.content || this.busy() || !this.progressDirty()) return;
    this.mutate(this.api.saveProgress(pack, this.progress()), 'saving', false);
  }

  remove(): void {
    const pack = this.selected();
    if (!pack || this.busy()) return;
    this.busy.set('deleting'); this.error.set(null);
    this.api.delete(pack.id).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(null))).subscribe({
      next: () => {
        this.packs.update(items => items.filter(p => p.id !== pack.id));
        this.selected.set(null); this.documentId.set(null); this.progress.set(emptyPreparationProgress());
        this.answerDrafts.set({}); this.helpResult.set(null);
        this.notice.set('Preparation pack deleted.');
      },
      error: error => this.error.set(this.readError(error, 'Could not delete this pack.'))
    });
  }

  savedAnswers(documentId: string): readonly PreparationAnswerInput[] {
    const discovery = this.selected()?.discovery;
    const questions = discovery?.documents.find(d => d.id === documentId)?.questions ?? [];
    return questions.map(q => {
      const answer = discovery?.answers.find(a => a.documentId === documentId && a.questionId === q.id);
      return { questionId: q.id, status: answer?.status ?? 'pending', text: answer?.text ?? '' };
    });
  }

  answers(documentId: string): readonly PreparationAnswerInput[] {
    return this.answerDrafts()[documentId] ?? this.savedAnswers(documentId);
  }

  answersDirty(documentId: string): boolean {
    return JSON.stringify(this.answers(documentId)) !== JSON.stringify(this.savedAnswers(documentId));
  }

  updateAnswer(documentId: string, questionId: string, patch: Partial<PreparationAnswerInput>): void {
    const answers = this.answers(documentId).map(a => a.questionId === questionId ? { ...a, ...patch } : a);
    this.answerDrafts.update(drafts => ({ ...drafts, [documentId]: answers }));
  }

  canWrite(documentId: string): boolean {
    const answers = this.answers(documentId);
    return !this.busy() && answers.length > 0 && answers.every(a => a.status !== 'pending'
      && (a.status !== 'answered' || !!a.text.trim()));
  }

  documentContextChanged(document: PreparationDocument): boolean {
    return document.isGenerated !== false && (document.contextRevision ?? 0) < (this.selected()?.discovery?.contextRevision ?? 0);
  }

  dayReady(documentIds: readonly string[]): boolean {
    return documentIds.every(id => this.selected()?.content?.documents.some(d => d.id === id && d.isGenerated !== false));
  }

  questionsNeedRefresh(documentId: string): boolean {
    const discovery = this.selected()?.discovery;
    const outline = discovery?.documents.find(d => d.id === documentId);
    return !!outline?.questions.length && this.selected()?.content?.documents.some(d => d.id === documentId && d.isGenerated === false) === true
      && (outline.contextRevision ?? 0) < (discovery?.contextRevision ?? 0)
      && !this.answersDirty(documentId) && !discovery?.answers.some(a => a.documentId === documentId && a.status !== 'pending');
  }

  prepareDocument(documentId: string): void {
    const pack = this.selected();
    if (!pack || this.busy()) return;
    this.documentMutation(this.api.prepareDocument(pack, documentId), 'questions', documentId);
  }

  saveAnswers(documentId: string): void {
    const pack = this.selected();
    if (!pack || this.busy()) return;
    this.documentMutation(this.api.saveAnswers(pack, documentId, this.answers(documentId)), 'answers', documentId);
  }

  writeDocument(documentId: string): void {
    const pack = this.selected();
    if (!pack || !this.canWrite(documentId)) return;
    // Save the user's explicit statuses first; guidance is never copied into this ledger.
    const request = (this.answersDirty(documentId)
      ? this.api.saveAnswers(pack, documentId, this.answers(documentId)).pipe(tap(saved => {
          this.acceptDocumentPack(saved); this.clearDraft(documentId); this.busy.set('writing');
        }))
      : of(pack)).pipe(switchMap(saved => this.api.writeDocument(saved, documentId)));
    this.documentMutation(request, this.answersDirty(documentId) ? 'answers' : 'writing', documentId, true);
  }

  askForHelp(documentId: string, questionId: string, request: string): void {
    const pack = this.selected();
    if (!pack || this.busy() || !request.trim()) return;
    this.busy.set('helping'); this.error.set(null);
    const draft = this.answers(documentId).find(a => a.questionId === questionId)?.text ?? '';
    const previous = this.helpResult();
    const previousGuidance = previous?.documentId === documentId && previous.questionId === questionId ? previous.response.markdown : undefined;
    this.api.help(pack, documentId, questionId, request.trim(), draft, previousGuidance)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(null))).subscribe({
        next: response => this.helpResult.set({ documentId, questionId, response }),
        error: error => this.error.set(this.readError(error, 'Could not load more guidance. Your answer is unchanged.'))
      });
  }

  private documentMutation(request: Observable<PreparationPack>, busy: 'questions' | 'answers' | 'writing', documentId: string, written = false): void {
    this.busy.set(busy); this.error.set(null); this.notice.set(null);
    request.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(null))).subscribe({
      next: pack => {
        this.acceptDocumentPack(pack); this.clearDraft(documentId);
        if (written) {
          const days = new Set(pack.content?.studyPlan.filter(s => s.documentIds.includes(documentId)).map(s => s.day));
          this.progress.update(p => ({ ...p,
            readDocumentIds: p.readDocumentIds.filter(id => id !== documentId),
            explainedDocumentIds: p.explainedDocumentIds.filter(id => id !== documentId),
            demonstratedDocumentIds: p.demonstratedDocumentIds.filter(id => id !== documentId),
            adaptedDocumentIds: p.adaptedDocumentIds.filter(id => id !== documentId),
            completedDays: p.completedDays.filter(day => !days.has(day))
          }));
        }
        this.notice.set(written ? 'This document is ready. Other documents were preserved.'
          : busy === 'questions' ? 'Clarification questions are ready.' : 'Your context is saved and available to the other documents.');
      },
      error: error => this.error.set(this.readError(error, 'Could not complete this step. Saved documents and answers were preserved.'))
    });
  }

  private acceptDocumentPack(pack: PreparationPack): void {
    this.selected.set(pack);
    this.packs.update(items => [pack, ...items.filter(p => p.id !== pack.id)]);
  }

  private clearDraft(documentId: string): void {
    this.answerDrafts.update(drafts => {
      const next = { ...drafts }; delete next[documentId]; return next;
    });
  }

  private mutate(request: Observable<PreparationPack>, busy: 'generating' | 'regenerating' | 'saving', resetDocument: boolean): void {
    if (this.busy()) return;
    this.busy.set(busy); this.error.set(null); this.notice.set(null);
    request.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(null))).subscribe({
      next: pack => {
        this.selected.set(pack); this.progress.set(pack.progress);
        this.packs.update(items => [pack, ...items.filter(p => p.id !== pack.id)]);
        if (resetDocument) { this.documentId.set(null); this.answerDrafts.set({}); this.helpResult.set(null); }
        this.notice.set(busy === 'saving' ? 'Progress saved.' : 'Your outline is ready. Open a document to shape it with a few clarification questions.');
      },
      error: error => {
        this.error.set(this.readError(error, 'Could not save preparation. Existing documents were preserved.'));
        if (error instanceof HttpErrorResponse && error.status === 409 && error.error?.existingPackId)
          this.notice.set('Reload preparation to open the existing pack.');
      }
    });
  }

  private readError(error: unknown, fallback: string): string {
    return error instanceof HttpErrorResponse && typeof error.error?.message === 'string' ? error.error.message : fallback;
  }
}
