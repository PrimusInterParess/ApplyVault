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
  DEFAULT_INTERVIEW_PREP_LANGUAGE_MIX,
  DEFAULT_INTERVIEW_PREP_MODE,
  INTERVIEW_PREP_START_MESSAGE,
  InterviewPrepChatMessage,
  InterviewPrepInference,
  InterviewPrepLanguageMix,
  InterviewPrepMode,
  InterviewPrepPriorTurn,
  InterviewPrepScorecard,
  InterviewPrepTurnResponse
} from '../models/interview-prep.model';

export type InterviewPrepCvGateStatus = 'unknown' | 'loading' | 'ready' | 'missing' | 'error';

export type InterviewPrepJobLinkStatus = 'idle' | 'loading' | 'ready' | 'invalid' | 'error';

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
  private turnSubscription: Subscription | null = null;
  private messageSequence = 0;
  private pendingJobId: string | null = null;

  readonly cvGateStatus = signal<InterviewPrepCvGateStatus>('unknown');
  readonly cvGateError = signal<string | null>(null);

  readonly mode = signal<InterviewPrepMode>(DEFAULT_INTERVIEW_PREP_MODE);
  readonly languageMix = signal<InterviewPrepLanguageMix>(DEFAULT_INTERVIEW_PREP_LANGUAGE_MIX);
  readonly scrapeResultId = signal<string | null>(null);

  readonly jobOptions = signal<readonly InterviewPrepJobOption[]>([]);
  readonly jobLinkStatus = signal<InterviewPrepJobLinkStatus>('idle');
  readonly jobLinkError = signal<string | null>(null);

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

  readonly sessionStarted = computed(() => this.messages().length > 0);
  readonly linkedJob = computed(() => {
    const id = this.scrapeResultId();
    if (!id) {
      return null;
    }

    return this.jobOptions().find((job) => job.id === id) ?? null;
  });
  readonly canSend = computed(() => {
    const draft = this.draftMessage().trim();
    return (
      this.cvGateStatus() === 'ready' &&
      !this.sending() &&
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

  setMode(mode: InterviewPrepMode): void {
    if (this.sessionStarted() || this.sending()) {
      return;
    }

    this.mode.set(mode);
  }

  setLanguageMix(languageMix: InterviewPrepLanguageMix): void {
    if (this.sessionStarted() || this.sending()) {
      return;
    }

    this.languageMix.set(languageMix);
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
    if (this.sessionStarted() || this.sending()) {
      return;
    }

    this.setScrapeResultIdFromJobId(jobId);
  }

  clearJobLink(): void {
    if (this.sessionStarted() || this.sending()) {
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
        // Keep pending scrapeResultId when list fails — server still validates on turn.
      }
    });
  }

  setDraftMessage(value: string): void {
    this.draftMessage.set(value);
  }

  startSession(): void {
    if (this.sessionStarted() || this.sending() || this.cvGateStatus() !== 'ready') {
      return;
    }

    this.sendTurn(INTERVIEW_PREP_START_MESSAGE);
  }

  sendDraft(): void {
    const text = this.draftMessage().trim();

    if (!text || this.sending() || this.cvGateStatus() !== 'ready') {
      return;
    }

    this.draftMessage.set('');
    this.sendTurn(text);
  }

  resetSession(): void {
    this.cancelTurn();
    this.sending.set(false);
    this.turnError.set(null);
    this.draftMessage.set('');
    this.messages.set([]);
    this.priorTurns.set([]);
    this.phase.set('interview');
    this.inference.set(null);
    this.scorecard.set(null);
    this.followUps.set([]);
    this.debriefBullets.set([]);
  }

  clearTurnError(): void {
    this.turnError.set(null);
  }

  private sendTurn(userMessage: string): void {
    this.cancelTurn();
    this.sending.set(true);
    this.turnError.set(null);

    const priorSnapshot = this.priorTurns();
    const userChat = this.createChatMessage('user', userMessage, this.phase());
    this.messages.update((current) => [...current, userChat]);

    this.turnSubscription = this.apiService
      .createTurn({
        mode: this.mode(),
        languageMix: this.languageMix(),
        userMessage,
        scrapeResultId: this.scrapeResultId(),
        priorTurns: priorSnapshot
      })
      .subscribe({
        next: (response) => {
          this.applySuccessfulTurn(userMessage, response);
          this.sending.set(false);
          this.turnSubscription = null;
        },
        error: (error: unknown) => {
          this.messages.update((current) => current.filter((message) => message.id !== userChat.id));
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
      { role: 'coach', text: response.coachMessage, phase: coachPhase === 'debrief' ? 'debrief' : 'interview' }
    ]);
    this.phase.set(coachPhase);
    this.inference.set(response.inference);
    this.scorecard.set(response.scorecard);
    this.followUps.set(response.followUps);
    this.debriefBullets.set(response.debriefBullets);
  }

  private mapTurnError(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 404) {
      return resolveHttpErrorMessage(error, {
        fallback:
          'Structured CV or linked job was not found. Open CV Builder to create your CV, or clear the job link.',
        statusMessages: {
          404: 'Structured CV or linked job was not found. Open CV Builder to create your CV, or clear the job link.'
        }
      });
    }

    if (error instanceof HttpErrorResponse && error.status === 400) {
      return resolveHttpErrorMessage(error, {
        fallback:
          'Interview Prep could not continue. Check that AI is enabled and your Structured CV has usable content.',
        statusMessages: {
          400: 'Interview Prep is unavailable right now (validation or AI off). Confirm AI is enabled and your CV has content.'
        }
      });
    }

    return resolveHttpErrorMessage(error, {
      fallback: 'Interview Prep could not complete this turn. Please try again.'
    });
  }

  private createChatMessage(
    role: InterviewPrepChatMessage['role'],
    text: string,
    phase: string
  ): InterviewPrepChatMessage {
    this.messageSequence += 1;

    return {
      id: `msg-${this.messageSequence}`,
      role,
      text,
      phase
    };
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

    // List failed: keep deep-link id for the turn request; server re-validates tenancy.
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

    // Not in owned list — do not send unknown id on turns.
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

  private cancelTurn(): void {
    this.turnSubscription?.unsubscribe();
    this.turnSubscription = null;
  }
}
