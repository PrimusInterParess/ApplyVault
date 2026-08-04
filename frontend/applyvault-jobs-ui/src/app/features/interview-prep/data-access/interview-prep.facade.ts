import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { resolveHttpErrorMessage } from '../../../core/http/api-error-message';
import { isRequestAborted } from '../../../core/http/is-request-aborted';
import { CvDocumentApiService } from '../../cv-projects/data-access/cv-document-api.service';
import { JobResultsApiService } from '../../job-results/data-access/job-results-api.service';
import { mapSavedJobResultToViewModel } from '../../job-results/utils/job-result.mapper';
import { InterviewPrepApiService } from './interview-prep-api.service';
import {
  DEFAULT_INTERVIEW_PREP_HIRING_MARKET,
  DEFAULT_INTERVIEW_PREP_LANGUAGE_MIX,
  DEFAULT_INTERVIEW_PREP_MODE,
  INTERVIEW_PREP_START_MESSAGE,
  InterviewPrepChatMessage,
  InterviewPrepHiringMarket,
  InterviewPrepInference,
  InterviewPrepLanguageMix,
  InterviewPrepMode,
  InterviewPrepPriorTurn,
  InterviewPrepScorecard,
  InterviewPrepSessionDetail,
  InterviewPrepSessionStatus,
  InterviewPrepSessionSummary,
  InterviewPrepTurnResponse
} from '../models/interview-prep.model';

export type InterviewPrepCvGateStatus = 'unknown' | 'loading' | 'ready' | 'missing' | 'error';

export type InterviewPrepJobLinkStatus = 'idle' | 'loading' | 'ready' | 'invalid' | 'error';

export type InterviewPrepHistoryStatus = 'idle' | 'loading' | 'ready' | 'error';

export type InterviewPrepSessionLoadStatus = 'idle' | 'loading' | 'error';

export interface InterviewPrepJobOption {
  readonly id: string;
  readonly title: string;
  readonly company: string;
  readonly label: string;
}

@Injectable({ providedIn: 'root' })
export class InterviewPrepFacade {
  private readonly apiService = inject(InterviewPrepApiService);
  private readonly cvDocumentApi = inject(CvDocumentApiService);
  private readonly jobResultsApi = inject(JobResultsApiService);

  private cvGateSubscription: Subscription | null = null;
  private jobsSubscription: Subscription | null = null;
  private historySubscription: Subscription | null = null;
  private sessionLoadSubscription: Subscription | null = null;
  private createSessionSubscription: Subscription | null = null;
  private deleteSubscription: Subscription | null = null;
  private turnSubscription: Subscription | null = null;
  private messageSequence = 0;
  private pendingJobId: string | null = null;

  readonly cvGateStatus = signal<InterviewPrepCvGateStatus>('unknown');
  readonly cvGateError = signal<string | null>(null);

  readonly mode = signal<InterviewPrepMode>(DEFAULT_INTERVIEW_PREP_MODE);
  readonly languageMix = signal<InterviewPrepLanguageMix>(DEFAULT_INTERVIEW_PREP_LANGUAGE_MIX);
  readonly hiringMarket = signal<InterviewPrepHiringMarket>(DEFAULT_INTERVIEW_PREP_HIRING_MARKET);
  readonly scrapeResultId = signal<string | null>(null);

  readonly jobOptions = signal<readonly InterviewPrepJobOption[]>([]);
  readonly jobLinkStatus = signal<InterviewPrepJobLinkStatus>('idle');
  readonly jobLinkError = signal<string | null>(null);

  readonly historyStatus = signal<InterviewPrepHistoryStatus>('idle');
  readonly historyError = signal<string | null>(null);
  readonly historyItems = signal<readonly InterviewPrepSessionSummary[]>([]);
  readonly historyTotalCount = signal(0);

  readonly sessionId = signal<string | null>(null);
  readonly sessionStatus = signal<InterviewPrepSessionStatus | string | null>(null);
  readonly sessionLoadStatus = signal<InterviewPrepSessionLoadStatus>('idle');
  readonly sessionLoadError = signal<string | null>(null);
  readonly deletingSessionId = signal<string | null>(null);

  readonly draftMessage = signal('');
  readonly sending = signal(false);
  readonly turnError = signal<string | null>(null);

  readonly messages = signal<readonly InterviewPrepChatMessage[]>([]);
  readonly priorTurns = signal<readonly InterviewPrepPriorTurn[]>([]);
  readonly phase = signal<string>('interview');
  readonly inference = signal<InterviewPrepInference | null>(null);
  readonly scorecard = signal<InterviewPrepScorecard | null>(null);
  readonly followUps = signal<readonly string[]>([]);
  readonly debriefBullets = signal<readonly string[]>([]);
  readonly modelAnswer = signal<string | null>(null);
  readonly modelAnswerRevealed = signal(false);

  readonly sessionStarted = computed(
    () => this.sessionId() != null || this.messages().length > 0
  );
  readonly isReadOnly = computed(
    () => this.sessionStatus() === 'completed' || this.phase() === 'debrief'
  );
  readonly linkedJob = computed(() => {
    const id = this.scrapeResultId();
    if (!id) {
      return null;
    }

    return this.jobOptions().find((job) => job.id === id) ?? null;
  });
  readonly isDebrief = computed(() => this.phase() === 'debrief');
  readonly setupLocked = computed(
    () => this.sessionStarted() || this.sending() || this.sessionLoadStatus() === 'loading'
  );

  readonly canSend = computed(() => {
    const draft = this.draftMessage().trim();
    return (
      this.cvGateStatus() === 'ready' &&
      !this.sending() &&
      !this.isReadOnly() &&
      this.sessionLoadStatus() !== 'loading' &&
      (this.sessionStarted() ? draft.length > 0 : true)
    );
  });

  loadCvGate(): void {
    this.cancelCvGate();
    this.cvGateStatus.set('loading');
    this.cvGateError.set(null);

    this.cvGateSubscription = this.cvDocumentApi.getStructured().subscribe({
      next: (document) => {
        const hasSections = Array.isArray(document.sections) && document.sections.length > 0;
        this.cvGateStatus.set(hasSections ? 'ready' : 'missing');
        this.cvGateSubscription = null;
      },
      error: (error: unknown) => {
        this.cvGateSubscription = null;

        if (isRequestAborted(error)) {
          return;
        }

        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.cvGateStatus.set('missing');
          return;
        }

        this.cvGateStatus.set('error');
        this.cvGateError.set(
          resolveHttpErrorMessage(error, {
            fallback: 'Could not verify your Structured CV. Try again in a moment.'
          })
        );
      }
    });
  }

  loadHistory(): void {
    this.cancelHistory();
    this.historyStatus.set('loading');
    this.historyError.set(null);

    this.historySubscription = this.apiService.listSessions({ take: 50, skip: 0 }).subscribe({
      next: (response) => {
        this.historyItems.set(response.items);
        this.historyTotalCount.set(response.totalCount);
        this.historyStatus.set('ready');
        this.historySubscription = null;
      },
      error: (error: unknown) => {
        this.historySubscription = null;

        if (isRequestAborted(error)) {
          return;
        }

        this.historyStatus.set('error');
        this.historyError.set(
          resolveHttpErrorMessage(error, {
            fallback: 'Could not load practice history. Try again in a moment.'
          })
        );
      }
    });
  }

  setMode(mode: InterviewPrepMode): void {
    if (this.setupLocked()) {
      return;
    }

    this.mode.set(mode);
  }

  setLanguageMix(languageMix: InterviewPrepLanguageMix): void {
    if (this.setupLocked()) {
      return;
    }

    this.languageMix.set(languageMix);
  }

  setHiringMarket(hiringMarket: InterviewPrepHiringMarket): void {
    if (this.setupLocked()) {
      return;
    }

    this.hiringMarket.set(hiringMarket);
  }

  /**
   * Maps deep-link / picker `jobId` → API `scrapeResultId`.
   * Resolves company/title once owned jobs are loaded; invalid ids clear targeting.
   */
  setScrapeResultIdFromJobId(jobId: string | null | undefined): void {
    const trimmed = jobId?.trim() || null;
    this.pendingJobId = trimmed;
    this.scrapeResultId.set(trimmed);
    this.resolvePendingJobLink();
  }

  selectOwnedJob(jobId: string | null | undefined): void {
    if (this.setupLocked()) {
      return;
    }

    this.setScrapeResultIdFromJobId(jobId);
  }

  clearJobLink(): void {
    if (this.setupLocked()) {
      return;
    }

    this.setScrapeResultIdFromJobId(null);
  }

  loadOwnedJobs(): void {
    this.cancelJobs();
    this.jobLinkStatus.set('loading');
    this.jobLinkError.set(null);

    this.jobsSubscription = this.jobResultsApi.getAll().subscribe({
      next: (results) => {
        const options = [...results]
          .map(mapSavedJobResultToViewModel)
          .sort(
            (left, right) =>
              new Date(right.savedAt).getTime() - new Date(left.savedAt).getTime()
          )
          .map(
            (job): InterviewPrepJobOption => ({
              id: job.id,
              title: job.title,
              company: job.company,
              label: `${job.company} · ${job.title}`
            })
          );

        this.jobOptions.set(options);
        this.jobsSubscription = null;
        this.jobLinkStatus.set('ready');
        this.resolvePendingJobLink();
      },
      error: (error: unknown) => {
        this.jobsSubscription = null;

        if (isRequestAborted(error)) {
          return;
        }

        this.jobOptions.set([]);
        this.jobLinkStatus.set('error');
        this.jobLinkError.set(
          resolveHttpErrorMessage(error, {
            fallback: 'Could not load your saved jobs. You can still practice without a job link.'
          })
        );
        // Keep pending scrapeResultId when list fails — server still validates on create/turn.
      }
    });
  }

  setDraftMessage(value: string): void {
    this.draftMessage.set(value);
  }

  /** Create a durable session, then bootstrap the first coach turn with sessionId. */
  startSession(): void {
    if (this.sessionStarted() || this.sending() || this.cvGateStatus() !== 'ready') {
      return;
    }

    this.cancelCreateSession();
    this.cancelTurn();
    this.sending.set(true);
    this.turnError.set(null);
    this.sessionLoadError.set(null);

    this.createSessionSubscription = this.apiService
      .createSession({
        mode: this.mode(),
        languageMix: this.languageMix(),
        hiringMarket: this.hiringMarket(),
        scrapeResultId: this.scrapeResultId()
      })
      .subscribe({
        next: (session) => {
          this.createSessionSubscription = null;
          this.sessionId.set(session.id);
          this.sessionStatus.set(session.status || 'in_progress');
          this.phase.set(session.phase || 'interview');
          // Bootstrap phrase is API-only — do not render as a "You" chat bubble.
          this.sendTurn(INTERVIEW_PREP_START_MESSAGE, {
            showUserMessage: false,
            alreadySending: true
          });
          this.loadHistory();
        },
        error: (error: unknown) => {
          this.createSessionSubscription = null;
          this.sending.set(false);

          if (isRequestAborted(error)) {
            return;
          }

          this.turnError.set(this.mapSessionError(error, 'create'));
        }
      });
  }

  openSession(sessionId: string): void {
    const trimmed = sessionId?.trim();
    if (!trimmed || this.sending() || this.sessionLoadStatus() === 'loading') {
      return;
    }

    this.cancelSessionLoad();
    this.cancelCreateSession();
    this.cancelTurn();
    this.clearActivePracticeState({ keepSetup: false });
    this.sessionLoadStatus.set('loading');
    this.sessionLoadError.set(null);
    this.turnError.set(null);

    this.sessionLoadSubscription = this.apiService.getSession(trimmed).subscribe({
      next: (detail) => {
        this.hydrateFromDetail(detail);
        this.sessionLoadStatus.set('idle');
        this.sessionLoadSubscription = null;
      },
      error: (error: unknown) => {
        this.sessionLoadSubscription = null;
        this.sessionLoadStatus.set('error');

        if (isRequestAborted(error)) {
          return;
        }

        this.sessionLoadError.set(this.mapSessionError(error, 'load'));
      }
    });
  }

  deleteSession(sessionId: string): void {
    const trimmed = sessionId?.trim();
    if (!trimmed || this.deletingSessionId()) {
      return;
    }

    this.cancelDelete();
    this.deletingSessionId.set(trimmed);

    this.deleteSubscription = this.apiService.deleteSession(trimmed).subscribe({
      next: () => {
        this.deleteSubscription = null;
        this.deletingSessionId.set(null);
        this.historyError.set(null);
        this.historyItems.update((items) => items.filter((item) => item.id !== trimmed));
        this.historyTotalCount.update((count) => Math.max(0, count - 1));

        if (this.sessionId() === trimmed) {
          this.resetSession();
        }
      },
      error: (error: unknown) => {
        this.deleteSubscription = null;
        this.deletingSessionId.set(null);

        if (isRequestAborted(error)) {
          return;
        }

        // Keep existing list visible; surface delete failure on the history panel.
        this.historyError.set(this.mapSessionError(error, 'delete'));
        if (this.historyStatus() !== 'ready') {
          this.historyStatus.set('error');
        }
      }
    });
  }

  sendDraft(): void {
    const text = this.draftMessage().trim();

    if (
      !text ||
      this.sending() ||
      this.cvGateStatus() !== 'ready' ||
      this.isReadOnly() ||
      !this.sessionId()
    ) {
      return;
    }

    this.draftMessage.set('');
    this.sendTurn(text);
  }

  /** Insert a suggested follow-up into the composer (does not send). */
  insertFollowUp(text: string): void {
    if (this.sending() || this.isReadOnly()) {
      return;
    }

    const trimmed = text.trim();
    if (!trimmed) {
      return;
    }

    this.draftMessage.set(trimmed);
  }

  /** Toggle visibility of the latest-turn answer guide (client-only; not chat/composer). */
  revealModelAnswer(): void {
    if (!this.modelAnswer() || this.isDebrief()) {
      return;
    }

    this.modelAnswerRevealed.update((current) => !current);
  }

  resetSession(): void {
    this.cancelTurn();
    this.cancelCreateSession();
    this.cancelSessionLoad();
    this.clearActivePracticeState({ keepSetup: true });
  }

  clearTurnError(): void {
    this.turnError.set(null);
  }

  clearSessionLoadError(): void {
    this.sessionLoadError.set(null);
    this.sessionLoadStatus.set('idle');
  }

  private sendTurn(
    userMessage: string,
    options: { readonly showUserMessage?: boolean; readonly alreadySending?: boolean } = {}
  ): void {
    const sessionId = this.sessionId();
    if (!sessionId) {
      this.sending.set(false);
      this.turnError.set('Practice session is missing. Start a new round.');
      return;
    }

    const showUserMessage = options.showUserMessage !== false;

    if (!options.alreadySending) {
      this.cancelTurn();
      this.sending.set(true);
    }

    this.turnError.set(null);

    const priorSnapshot = this.priorTurns();
    const userChat = showUserMessage
      ? this.createChatMessage('user', userMessage, this.phase())
      : null;

    if (userChat) {
      this.messages.update((current) => [...current, userChat]);
    }

    this.turnSubscription = this.apiService
      .createTurn({
        mode: this.mode(),
        languageMix: this.languageMix(),
        hiringMarket: this.hiringMarket(),
        userMessage,
        scrapeResultId: this.scrapeResultId(),
        priorTurns: priorSnapshot,
        sessionId
      })
      .subscribe({
        next: (response) => {
          this.applySuccessfulTurn(userMessage, response);
          this.sending.set(false);
          this.turnSubscription = null;
          this.loadHistory();
        },
        error: (error: unknown) => {
          if (userChat) {
            this.messages.update((current) =>
              current.filter((message) => message.id !== userChat.id)
            );
          }
          this.sending.set(false);
          this.turnSubscription = null;

          if (isRequestAborted(error)) {
            return;
          }

          this.turnError.set(this.mapTurnError(error));
        }
      });
  }

  private applySuccessfulTurn(userMessage: string, response: InterviewPrepTurnResponse): void {
    const coachPhase = response.phase || 'interview';
    const coachChat = this.createChatMessage('coach', response.coachMessage, coachPhase);

    this.messages.update((current) => [...current, coachChat]);
    this.priorTurns.update((current) => [
      ...current,
      { role: 'user', text: userMessage, phase: 'interview' },
      {
        role: 'coach',
        text: response.coachMessage,
        phase: coachPhase === 'debrief' ? 'debrief' : 'interview'
      }
    ]);
    this.phase.set(coachPhase);
    this.inference.set(response.inference);
    // Null scorecard on setup/continuation turns must not wipe a prior scorecard.
    if (response.scorecard) {
      this.scorecard.set(response.scorecard);
    }
    this.followUps.set(response.followUps);
    this.debriefBullets.set(response.debriefBullets);
    this.modelAnswer.set(response.modelAnswer);
    this.modelAnswerRevealed.set(false);

    if (response.sessionId) {
      this.sessionId.set(response.sessionId);
    }

    if (coachPhase === 'debrief') {
      this.sessionStatus.set('completed');
    }
  }

  private hydrateFromDetail(detail: InterviewPrepSessionDetail): void {
    this.sessionId.set(detail.id);
    this.sessionStatus.set(detail.status || 'in_progress');
    this.mode.set(this.asMode(detail.mode));
    this.languageMix.set(this.asLanguageMix(detail.languageMix));
    this.hiringMarket.set(this.asHiringMarket(detail.hiringMarket));
    this.scrapeResultId.set(detail.scrapeResultId);
    this.pendingJobId = detail.scrapeResultId;
    this.resolvePendingJobLink();

    const chatMessages: InterviewPrepChatMessage[] = [];
    const prior: InterviewPrepPriorTurn[] = [];
    let latestScorecard: InterviewPrepScorecard | null = null;
    let latestFollowUps: readonly string[] = [];
    let latestDebrief: readonly string[] = [];
    let latestModelAnswer: string | null = null;
    let latestInference: InterviewPrepInference | null = null;
    let latestPhase = detail.phase || 'interview';

    for (const message of detail.messages) {
      const role = message.role === 'user' ? 'user' : 'coach';
      const phase = message.phase || 'interview';
      prior.push({ role, text: message.text, phase: phase === 'debrief' ? 'debrief' : 'interview' });

      const hideBootstrap =
        role === 'user' && message.text.trim() === INTERVIEW_PREP_START_MESSAGE;

      if (!hideBootstrap && message.text.trim().length > 0) {
        chatMessages.push({
          id: message.id || this.nextMessageId(),
          role,
          text: message.text,
          phase
        });
      }

      if (role === 'coach') {
        latestPhase = phase;
        if (message.scorecard) {
          latestScorecard = message.scorecard;
        }
        latestFollowUps = message.followUps;
        latestDebrief = message.debriefBullets;
        latestModelAnswer = message.modelAnswer;
        if (message.inference) {
          latestInference = message.inference;
        }
      }
    }

    this.messages.set(chatMessages);
    this.priorTurns.set(prior);
    this.phase.set(latestPhase);
    this.inference.set(latestInference);
    this.scorecard.set(latestScorecard);
    this.followUps.set(detail.status === 'completed' ? [] : latestFollowUps);
    this.debriefBullets.set(latestDebrief);
    this.modelAnswer.set(detail.status === 'completed' ? null : latestModelAnswer);
    this.modelAnswerRevealed.set(false);
    this.draftMessage.set('');
    this.sending.set(false);
  }

  private clearActivePracticeState(options: { readonly keepSetup: boolean }): void {
    this.sending.set(false);
    this.turnError.set(null);
    this.sessionLoadError.set(null);
    this.sessionLoadStatus.set('idle');
    this.draftMessage.set('');
    this.messages.set([]);
    this.priorTurns.set([]);
    this.phase.set('interview');
    this.inference.set(null);
    this.scorecard.set(null);
    this.followUps.set([]);
    this.debriefBullets.set([]);
    this.modelAnswer.set(null);
    this.modelAnswerRevealed.set(false);
    this.sessionId.set(null);
    this.sessionStatus.set(null);

    if (!options.keepSetup) {
      // Mode / language / job are overwritten by fetch hydration.
      return;
    }
  }

  private mapTurnError(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 409) {
      return resolveHttpErrorMessage(error, {
        fallback: 'This practice session is complete and read-only. Start a new round to continue.',
        statusMessages: {
          409: 'This practice session is complete and read-only. Start a new round to continue.'
        }
      });
    }

    if (error instanceof HttpErrorResponse && error.status === 404) {
      return resolveHttpErrorMessage(error, {
        fallback:
          'Structured CV, session, or linked job was not found. Open CV Builder, refresh history, or clear the job link.',
        statusMessages: {
          404: 'Structured CV, session, or linked job was not found. Open CV Builder, refresh history, or clear the job link.'
        }
      });
    }

    if (error instanceof HttpErrorResponse && error.status === 400) {
      return resolveHttpErrorMessage(error, {
        fallback:
          'Interview Prep could not continue. Check that AI is enabled and your Structured CV has usable content.',
        statusMessages: {
          400: 'Interview Prep is unavailable right now (validation, message cap, or AI off). Confirm AI is enabled and your CV has content.'
        }
      });
    }

    return resolveHttpErrorMessage(error, {
      fallback: 'Interview Prep could not complete this turn. Please try again.'
    });
  }

  private mapSessionError(
    error: unknown,
    action: 'create' | 'load' | 'delete'
  ): string {
    const fallbacks: Record<typeof action, string> = {
      create: 'Could not create a practice session. Please try again.',
      load: 'Could not open that practice session. It may have been deleted.',
      delete: 'Could not delete that practice session. Please try again.'
    };

    if (error instanceof HttpErrorResponse && error.status === 404) {
      return resolveHttpErrorMessage(error, {
        fallback:
          action === 'create'
            ? 'Linked job was not found. Continue as general prep or pick another job.'
            : fallbacks[action],
        statusMessages: {
          404:
            action === 'create'
              ? 'Linked job was not found. Continue as general prep or pick another job.'
              : fallbacks[action]
        }
      });
    }

    return resolveHttpErrorMessage(error, {
      fallback: fallbacks[action]
    });
  }

  private createChatMessage(
    role: InterviewPrepChatMessage['role'],
    text: string,
    phase: string
  ): InterviewPrepChatMessage {
    return {
      id: this.nextMessageId(),
      role,
      text,
      phase
    };
  }

  private nextMessageId(): string {
    this.messageSequence += 1;
    return `msg-${this.messageSequence}`;
  }

  private asMode(value: string): InterviewPrepMode {
    const allowed: readonly InterviewPrepMode[] = [
      'screening',
      'behavioral',
      'role_domain',
      'problem_solving',
      'process_systems',
      'language_practice',
      'full_loop'
    ];
    return (allowed.includes(value as InterviewPrepMode)
      ? value
      : DEFAULT_INTERVIEW_PREP_MODE) as InterviewPrepMode;
  }

  private asLanguageMix(value: string): InterviewPrepLanguageMix {
    const allowed: readonly InterviewPrepLanguageMix[] = ['en', 'da', 'mixed'];
    return (allowed.includes(value as InterviewPrepLanguageMix)
      ? value
      : DEFAULT_INTERVIEW_PREP_LANGUAGE_MIX) as InterviewPrepLanguageMix;
  }

  private asHiringMarket(value: string): InterviewPrepHiringMarket {
    const allowed: readonly InterviewPrepHiringMarket[] = ['general', 'dk'];
    return (allowed.includes(value as InterviewPrepHiringMarket)
      ? value
      : DEFAULT_INTERVIEW_PREP_HIRING_MARKET) as InterviewPrepHiringMarket;
  }

  private resolvePendingJobLink(): void {
    const pending = this.pendingJobId;
    const status = this.jobLinkStatus();

    if (status === 'loading') {
      return;
    }

    if (!pending) {
      this.scrapeResultId.set(null);
      this.jobLinkError.set(null);
      if (status === 'invalid') {
        this.jobLinkStatus.set('ready');
      }
      return;
    }

    // List failed: keep deep-link id for create/turn; server re-validates tenancy.
    if (status === 'error' || status === 'idle') {
      this.scrapeResultId.set(pending);
      return;
    }

    const match = this.jobOptions().find((job) => job.id === pending);

    if (match) {
      this.scrapeResultId.set(match.id);
      this.jobLinkStatus.set('ready');
      this.jobLinkError.set(null);
      return;
    }

    // Not in owned list — do not send unknown id on create/turns when list is ready.
    // Resumed sessions may still hold a scrapeResultId snapshot even if the job list misses it.
    if (this.sessionId()) {
      this.scrapeResultId.set(pending);
      this.jobLinkStatus.set('ready');
      this.jobLinkError.set(null);
      return;
    }

    this.scrapeResultId.set(null);
    this.jobLinkStatus.set('invalid');
    this.jobLinkError.set(
      'That saved job could not be found in your workspace. Continue with general prep or pick another job.'
    );
  }

  private cancelCvGate(): void {
    this.cvGateSubscription?.unsubscribe();
    this.cvGateSubscription = null;
  }

  private cancelJobs(): void {
    this.jobsSubscription?.unsubscribe();
    this.jobsSubscription = null;
  }

  private cancelHistory(): void {
    this.historySubscription?.unsubscribe();
    this.historySubscription = null;
  }

  private cancelSessionLoad(): void {
    this.sessionLoadSubscription?.unsubscribe();
    this.sessionLoadSubscription = null;
  }

  private cancelCreateSession(): void {
    this.createSessionSubscription?.unsubscribe();
    this.createSessionSubscription = null;
  }

  private cancelDelete(): void {
    this.deleteSubscription?.unsubscribe();
    this.deleteSubscription = null;
  }

  private cancelTurn(): void {
    this.turnSubscription?.unsubscribe();
    this.turnSubscription = null;
  }
}
