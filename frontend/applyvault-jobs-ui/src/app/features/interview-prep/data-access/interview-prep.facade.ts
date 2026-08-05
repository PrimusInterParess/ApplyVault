import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { CvDocumentApiService } from '../../cv-projects/data-access/cv-document-api.service';
import { JobResultsApiService } from '../../job-results/data-access/job-results-api.service';
import { InterviewPrepApiService } from './interview-prep-api.service';
import {
  DEFAULT_INTERVIEW_PREP_EXPERIENCE_TYPE,
  DEFAULT_INTERVIEW_PREP_LANGUAGE,
  DEFAULT_INTERVIEW_PREP_MARKET,
  DEFAULT_INTERVIEW_PREP_MODE,
  defaultPersonaForMode,
  InterviewPrepAnswerRetryResult,
  InterviewPrepAnswerReview,
  InterviewPrepCandidateReport,
  InterviewPrepCompetencyResults,
  InterviewPrepExperienceType,
  InterviewPrepLanguage,
  InterviewPrepMarket,
  InterviewPrepMode,
  InterviewPrepPanelDebrief,
  InterviewPrepPersona,
  InterviewPrepSessionDetail,
  InterviewPrepSessionSummary,
  InterviewPrepTranscript,
  InterviewPrepTurn,
  isPersonaValidForMode,
  personasForMode
} from '../models/interview-prep.model';

export type InterviewPrepCvGateStatus = 'unknown' | 'loading' | 'ready' | 'missing' | 'error';

export type InterviewPrepJobLinkStatus = 'idle' | 'loading' | 'ready' | 'invalid' | 'error';

export type InterviewPrepHistoryStatus = 'idle' | 'loading' | 'ready' | 'error';

export type InterviewPrepSessionLoadStatus = 'idle' | 'loading' | 'ready' | 'error';

export type InterviewPrepResultsTab = 'report' | 'transcript' | 'competencies' | 'panel';

export interface InterviewPrepJobOption {
  readonly id: string;
  readonly label: string;
}

@Injectable({ providedIn: 'root' })
export class InterviewPrepFacade {
  private readonly api = inject(InterviewPrepApiService);
  private readonly cvDocumentApi = inject(CvDocumentApiService);
  private readonly jobResultsApi = inject(JobResultsApiService);

  private cvGateSubscription: Subscription | null = null;
  private historySubscription: Subscription | null = null;
  private sessionSubscription: Subscription | null = null;
  private flowSubscription: Subscription | null = null;
  private turnSubscription: Subscription | null = null;
  private resultsSubscription: Subscription | null = null;
  private coachingSubscription: Subscription | null = null;

  private pendingClientTurnId: string | null = null;

  readonly cvGateStatus = signal<InterviewPrepCvGateStatus>('unknown');
  readonly cvGateError = signal<string | null>(null);

  readonly mode = signal<InterviewPrepMode>(DEFAULT_INTERVIEW_PREP_MODE);
  readonly persona = signal<InterviewPrepPersona>(defaultPersonaForMode(DEFAULT_INTERVIEW_PREP_MODE));
  readonly language = signal<InterviewPrepLanguage>(DEFAULT_INTERVIEW_PREP_LANGUAGE);
  readonly market = signal<InterviewPrepMarket>(DEFAULT_INTERVIEW_PREP_MARKET);
  readonly experienceType = signal<InterviewPrepExperienceType>(DEFAULT_INTERVIEW_PREP_EXPERIENCE_TYPE);

  readonly jobOptions = signal<readonly InterviewPrepJobOption[]>([]);
  readonly selectedScrapeResultId = signal<string | null>(null);
  readonly jobLinkStatus = signal<InterviewPrepJobLinkStatus>('idle');
  readonly jobLinkError = signal<string | null>(null);

  readonly historyStatus = signal<InterviewPrepHistoryStatus>('idle');
  readonly historyError = signal<string | null>(null);
  readonly historyItems = signal<readonly InterviewPrepSessionSummary[]>([]);

  readonly sessionId = signal<string | null>(null);
  readonly sessionDetail = signal<InterviewPrepSessionDetail | null>(null);
  readonly sessionEtag = signal<string | null>(null);
  readonly sessionLoadStatus = signal<InterviewPrepSessionLoadStatus>('idle');
  readonly sessionError = signal<string | null>(null);

  readonly startingSession = signal(false);
  readonly submittingAnswer = signal(false);
  /** Last turn-submit reported a mid-loop Stage auto-advance (cleared on next submit). */
  readonly lastStageTransitionOccurred = signal(false);
  readonly lifecycleBusy = signal(false);
  readonly resultsTab = signal<InterviewPrepResultsTab>('report');

  readonly transcript = signal<InterviewPrepTranscript | null>(null);
  readonly report = signal<InterviewPrepCandidateReport | null>(null);
  readonly competencies = signal<InterviewPrepCompetencyResults | null>(null);
  readonly panelDebrief = signal<InterviewPrepPanelDebrief | null>(null);
  readonly resultsLoading = signal(false);
  readonly resultsError = signal<string | null>(null);

  readonly coachingReview = signal<InterviewPrepAnswerReview | null>(null);
  readonly coachingRetry = signal<InterviewPrepAnswerRetryResult | null>(null);
  readonly coachingTurnId = signal<string | null>(null);
  readonly coachingBusy = signal(false);
  readonly coachingError = signal<string | null>(null);

  readonly availablePersonas = computed(() => personasForMode(this.mode()));

  readonly chatTurns = computed((): readonly InterviewPrepTurn[] => {
    const turns = this.sessionDetail()?.turns ?? [];
    return turns.filter((turn) => turn.role !== 'system');
  });

  readonly canAnswer = computed(() => {
    const status = this.sessionDetail()?.status;
    return status === 'inProgress' && !this.submittingAnswer() && !this.lifecycleBusy();
  });

  readonly canCoach = computed(() => {
    const detail = this.sessionDetail();
    if (!detail) {
      return false;
    }
    const status = detail.status;
    if (detail.experienceType === 'guidedCoaching') {
      return status === 'inProgress' || status === 'paused' || status === 'completed' || status === 'completing';
    }
    return status === 'completed' || status === 'completing';
  });

  readonly showResults = computed(() => {
    const status = this.sessionDetail()?.status;
    return status === 'completing' || status === 'completed';
  });

  readonly isFullLoop = computed(() => this.sessionDetail()?.mode === 'fullLoop');

  readonly activeStageLabel = computed(() => {
    const stages = this.sessionDetail()?.stages ?? [];
    const active = stages.find((stage) => stage.status !== 'completed' && stage.status !== 'assessed');
    if (!active) {
      const last = stages[stages.length - 1];
      return last ? `${last.stageType} (${last.status})` : null;
    }
    return `${active.stageType} (${active.status})`;
  });

  /**
   * Recovery-only: prior Stage finished (completed/assessed) with no live Stage and a Planned next.
   * Happy path auto-advances via POST /turns — do not gate the interview on this control.
   */
  readonly canAdvanceFullLoop = computed(() => {
    const detail = this.sessionDetail();
    if (!detail || detail.mode !== 'fullLoop') {
      return false;
    }
    if (detail.status !== 'inProgress' && detail.status !== 'ready') {
      return false;
    }
    if (this.submittingAnswer() || this.lifecycleBusy()) {
      return false;
    }
    const stages = detail.stages;
    const hasActiveInterviewStage = stages.some((stage) => isActiveInterviewStageStatus(stage.status));
    if (hasActiveInterviewStage) {
      return false;
    }
    const hasCompletedPrior = stages.some(
      (stage) => stage.status === 'completed' || stage.status === 'assessed'
    );
    const hasPlannedNext = stages.some((stage) => stage.status === 'planned');
    return hasCompletedPrior && hasPlannedNext;
  });

  /**
   * Full-loop turn submit may auto-advance Stages (assessment + handoff + next open).
   * Status banner only — not a chat bubble.
   */
  readonly fullLoopTransitionBanner = computed((): string | null => {
    if (!this.isFullLoop() || !this.submittingAnswer()) {
      return null;
    }
    return 'Transitioning to the next interviewer…';
  });

  readonly allFullLoopStagesDone = computed(() => {
    const detail = this.sessionDetail();
    if (!detail || detail.mode !== 'fullLoop' || detail.stages.length === 0) {
      return false;
    }
    return detail.stages.every(
      (stage) => stage.status === 'completed' || stage.status === 'assessed'
    );
  });

  setMode(mode: InterviewPrepMode): void {
    this.mode.set(mode);
    if (!isPersonaValidForMode(mode, this.persona())) {
      this.persona.set(defaultPersonaForMode(mode));
    }
  }

  setPersona(persona: InterviewPrepPersona): void {
    if (isPersonaValidForMode(this.mode(), persona)) {
      this.persona.set(persona);
    }
  }

  setLanguage(language: InterviewPrepLanguage): void {
    this.language.set(language);
  }

  setMarket(market: InterviewPrepMarket): void {
    this.market.set(market);
  }

  setExperienceType(experienceType: InterviewPrepExperienceType): void {
    this.experienceType.set(experienceType);
  }

  setSelectedJob(scrapeResultId: string | null): void {
    this.selectedScrapeResultId.set(scrapeResultId);
  }

  applyJobIdFromQuery(jobId: string | null): void {
    if (!jobId) {
      return;
    }
    this.selectedScrapeResultId.set(jobId);
    this.jobLinkStatus.set('loading');
    this.jobLinkError.set(null);
    this.jobResultsApi.getAll().subscribe({
      next: (jobs) => {
        const match = jobs.find((job) => job.id === jobId);
        if (match) {
          this.jobLinkStatus.set('ready');
          this.jobOptions.set(this.mapJobOptions(jobs));
        } else {
          this.jobLinkStatus.set('invalid');
          this.jobLinkError.set('Saved job not found — pick another or continue without a job.');
          this.selectedScrapeResultId.set(null);
        }
      },
      error: (error) => {
        this.jobLinkStatus.set('error');
        this.jobLinkError.set(this.readError(error, 'Could not verify the linked job.'));
      }
    });
  }

  loadCvGate(): void {
    this.cvGateSubscription?.unsubscribe();
    this.cvGateStatus.set('loading');
    this.cvGateError.set(null);
    this.cvGateSubscription = this.cvDocumentApi.getStructured().subscribe({
      next: (document) => {
        const sections = document.sections?.length ?? 0;
        this.cvGateStatus.set(sections > 0 ? 'ready' : 'missing');
      },
      error: (error) => {
        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.cvGateStatus.set('missing');
          return;
        }
        this.cvGateStatus.set('error');
        this.cvGateError.set(this.readError(error, 'Could not load your Structured CV.'));
      }
    });
  }

  loadJobOptions(): void {
    this.jobLinkStatus.set('loading');
    this.jobLinkError.set(null);
    this.jobResultsApi.getAll().subscribe({
      next: (jobs) => {
        this.jobOptions.set(this.mapJobOptions(jobs));
        this.jobLinkStatus.set('ready');
      },
      error: (error) => {
        this.jobLinkStatus.set('error');
        this.jobLinkError.set(this.readError(error, 'Could not load saved jobs.'));
      }
    });
  }

  loadHistory(): void {
    this.historySubscription?.unsubscribe();
    this.historyStatus.set('loading');
    this.historyError.set(null);
    this.historySubscription = this.api.listSessions().subscribe({
      next: (response) => {
        this.historyItems.set(response.items ?? []);
        this.historyStatus.set('ready');
      },
      error: (error) => {
        this.historyStatus.set('error');
        this.historyError.set(this.readError(error, 'Could not load session history.'));
      }
    });
  }

  loadSession(id: string): void {
    this.sessionSubscription?.unsubscribe();
    this.sessionId.set(id);
    this.sessionLoadStatus.set('loading');
    this.sessionError.set(null);
    this.sessionSubscription = this.api.getSession(id).subscribe({
      next: ({ detail, eTag }) => {
        this.applySessionDetail(detail, eTag);
        this.sessionLoadStatus.set('ready');
        if (detail.status === 'completing' || detail.status === 'completed') {
          this.loadResults();
        }
        if (detail.mode === 'fullLoop' && this.allFullLoopStagesDone()) {
          this.loadPanelDebrief();
        }
      },
      error: (error) => {
        this.sessionLoadStatus.set('error');
        this.sessionError.set(this.readError(error, 'Could not load session.'));
      }
    });
  }

  continueLoadedSession(): void {
    const sessionId = this.sessionId();
    const detail = this.sessionDetail();
    const etag = this.sessionEtag();
    if (!sessionId || !detail || !etag) {
      return;
    }
    if (detail.status === 'ready') {
      this.lifecycleBusy.set(true);
      this.api.startSession(sessionId, etag).subscribe({
        next: (started) => {
          this.lifecycleBusy.set(false);
          this.applySessionDetail(started, started.eTag);
        },
        error: (error) => {
          this.lifecycleBusy.set(false);
          this.sessionError.set(this.mapSessionMutationError(error));
        }
      });
      return;
    }
    if (detail.status === 'created') {
      this.lifecycleBusy.set(true);
      this.api.prepareSession(sessionId, etag).subscribe({
        next: (prepared) => {
          this.api.startSession(sessionId, prepared.eTag).subscribe({
            next: (started) => {
              this.lifecycleBusy.set(false);
              this.applySessionDetail(started, started.eTag);
            },
            error: (error) => {
              this.lifecycleBusy.set(false);
              this.sessionError.set(this.mapSessionMutationError(error));
            }
          });
        },
        error: (error) => {
          this.lifecycleBusy.set(false);
          this.sessionError.set(this.mapSessionMutationError(error));
        }
      });
    }
  }

  startNewSession(): void {
    this.flowSubscription?.unsubscribe();
    this.startingSession.set(true);
    this.sessionError.set(null);
    const scrapeResultId = this.selectedScrapeResultId();
    this.flowSubscription = this.api
      .createSession({
        mode: this.mode(),
        persona: this.persona(),
        language: this.language(),
        market: this.market(),
        experienceType: this.experienceType(),
        interactionType: 'text',
        scrapeResultId: scrapeResultId ?? undefined,
        idempotencyKey: crypto.randomUUID()
      })
      .subscribe({
        next: (summary) => {
          this.sessionId.set(summary.id);
          this.prepareAndStart(summary.id);
        },
        error: (error) => {
          this.startingSession.set(false);
          this.sessionError.set(this.readError(error, 'Could not create session.'));
        }
      });
  }

  private prepareAndStart(sessionId: string): void {
    this.api.prepareSession(sessionId).subscribe({
      next: (detail) => {
        this.applySessionDetail(detail, detail.eTag);
        this.api.startSession(sessionId, detail.eTag).subscribe({
          next: (started) => {
            this.applySessionDetail(started, started.eTag);
            this.startingSession.set(false);
            this.loadHistory();
          },
          error: (error) => {
            this.startingSession.set(false);
            this.sessionError.set(this.readError(error, 'Could not start session.'));
          }
        });
      },
      error: (error) => {
        this.startingSession.set(false);
        this.sessionError.set(this.readError(error, 'Could not prepare session.'));
      }
    });
  }

  refreshSession(): void {
    const id = this.sessionId();
    if (id) {
      this.loadSession(id);
    }
  }

  submitAnswer(answer: string): void {
    const trimmed = answer.trim();
    if (!trimmed || !this.canAnswer()) {
      return;
    }
    const sessionId = this.sessionId();
    const etag = this.sessionEtag();
    if (!sessionId || !etag) {
      return;
    }
    const clientTurnId = this.pendingClientTurnId ?? crypto.randomUUID();
    this.pendingClientTurnId = clientTurnId;
    this.turnSubscription?.unsubscribe();
    this.submittingAnswer.set(true);
    this.lastStageTransitionOccurred.set(false);
    this.sessionError.set(null);
    this.turnSubscription = this.api
      .submitTurn(sessionId, { clientTurnId, answer: trimmed }, etag)
      .subscribe({
        next: ({ result, eTag }) => {
          this.pendingClientTurnId = null;
          this.submittingAnswer.set(false);
          this.lastStageTransitionOccurred.set(result.stageTransitionOccurred === true);
          // Prefer session.turns (includes stageHandoff + next-stage opening) over nextInterviewerTurn alone.
          this.applySessionDetail(result.session, eTag || result.session.eTag);
          if (result.interviewComplete || result.session.status === 'completing') {
            this.loadResults();
          }
          if (result.session.mode === 'fullLoop' && this.allFullLoopStagesDone()) {
            this.loadPanelDebrief();
          }
        },
        error: (error) => {
          this.submittingAnswer.set(false);
          this.lastStageTransitionOccurred.set(false);
          this.sessionError.set(this.mapSessionMutationError(error));
        }
      });
  }

  pauseSession(): void {
    this.runLifecycle((id, etag) => this.api.pauseSession(id, etag));
  }

  resumeSession(): void {
    this.runLifecycle((id, etag) => this.api.resumeSession(id, etag));
  }

  cancelSession(): void {
    this.runLifecycle((id, etag) => this.api.cancelSession(id, etag));
  }

  completeSession(): void {
    this.runLifecycle((id, etag) => this.api.completeSession(id, etag), () => this.loadResults());
  }

  advanceFullLoopStage(): void {
    const sessionId = this.sessionId();
    const etag = this.sessionEtag();
    if (!sessionId || !etag) {
      return;
    }
    this.lifecycleBusy.set(true);
    this.sessionError.set(null);
    this.api.startNextFullLoopStage(sessionId, etag).subscribe({
      next: (detail) => {
        this.lifecycleBusy.set(false);
        this.applySessionDetail(detail, detail.eTag);
      },
      error: (error) => {
        this.lifecycleBusy.set(false);
        this.sessionError.set(this.mapSessionMutationError(error));
      }
    });
  }

  loadResults(): void {
    const sessionId = this.sessionId();
    if (!sessionId) {
      return;
    }
    this.resultsSubscription?.unsubscribe();
    this.resultsLoading.set(true);
    this.resultsError.set(null);
    this.resultsSubscription = this.api.getReport(sessionId).subscribe({
      next: (report) => {
        this.report.set(report);
        this.resultsLoading.set(false);
      },
      error: (error) => {
        this.resultsLoading.set(false);
        this.resultsError.set(this.readError(error, 'Could not load report.'));
      }
    });
    this.api.getTranscript(sessionId).subscribe({
      next: (transcript) => this.transcript.set(transcript),
      error: () => undefined
    });
    this.api.getCompetencies(sessionId).subscribe({
      next: (competencies) => this.competencies.set(competencies),
      error: () => undefined
    });
  }

  loadPanelDebrief(): void {
    const sessionId = this.sessionId();
    if (!sessionId) {
      return;
    }
    this.api.getPanelDebrief(sessionId).subscribe({
      next: (debrief) => this.panelDebrief.set(debrief),
      error: (error) => {
        this.resultsError.set(this.readError(error, 'Could not load panel debrief.'));
      }
    });
  }

  openCoachingReview(candidateTurnId: string): void {
    const sessionId = this.sessionId();
    if (!sessionId || !this.canCoach()) {
      return;
    }
    this.coachingSubscription?.unsubscribe();
    this.coachingBusy.set(true);
    this.coachingError.set(null);
    this.coachingTurnId.set(candidateTurnId);
    this.coachingSubscription = this.api.requestAnswerReview(sessionId, candidateTurnId).subscribe({
      next: (review) => {
        this.coachingReview.set(review);
        this.coachingBusy.set(false);
      },
      error: (error) => {
        this.coachingBusy.set(false);
        this.coachingError.set(this.mapSessionMutationError(error));
      }
    });
  }

  submitCoachingRetry(revisedAnswer: string): void {
    const sessionId = this.sessionId();
    const turnId = this.coachingTurnId();
    if (!sessionId || !turnId || !revisedAnswer.trim()) {
      return;
    }
    this.coachingBusy.set(true);
    this.coachingError.set(null);
    this.api.submitAnswerRetry(sessionId, turnId, revisedAnswer.trim()).subscribe({
      next: (retry) => {
        this.coachingRetry.set(retry);
        this.coachingBusy.set(false);
      },
      error: (error) => {
        this.coachingBusy.set(false);
        this.coachingError.set(this.mapSessionMutationError(error));
      }
    });
  }

  clearCoaching(): void {
    this.coachingReview.set(null);
    this.coachingRetry.set(null);
    this.coachingTurnId.set(null);
    this.coachingError.set(null);
  }

  deleteHistorySession(id: string): void {
    this.api.deleteSession(id).subscribe({
      next: () => {
        this.historyItems.update((items) => items.filter((item) => item.id !== id));
        if (this.sessionId() === id) {
          this.resetActiveSession();
        }
      },
      error: (error) => {
        this.historyError.set(this.readError(error, 'Could not delete session.'));
      }
    });
  }

  resetActiveSession(): void {
    this.sessionId.set(null);
    this.sessionDetail.set(null);
    this.sessionEtag.set(null);
    this.sessionLoadStatus.set('idle');
    this.submittingAnswer.set(false);
    this.lastStageTransitionOccurred.set(false);
    this.transcript.set(null);
    this.report.set(null);
    this.competencies.set(null);
    this.panelDebrief.set(null);
    this.clearCoaching();
    this.pendingClientTurnId = null;
  }

  setResultsTab(tab: InterviewPrepResultsTab): void {
    this.resultsTab.set(tab);
    if (tab === 'panel' && this.isFullLoop()) {
      this.loadPanelDebrief();
    }
  }

  private runLifecycle(
    call: (id: string, etag: string) => ReturnType<InterviewPrepApiService['pauseSession']>,
    onSuccess?: () => void
  ): void {
    const sessionId = this.sessionId();
    const etag = this.sessionEtag();
    if (!sessionId || !etag) {
      return;
    }
    this.lifecycleBusy.set(true);
    this.sessionError.set(null);
    call(sessionId, etag).subscribe({
      next: (detail) => {
        this.lifecycleBusy.set(false);
        this.applySessionDetail(detail, detail.eTag);
        onSuccess?.();
        this.loadHistory();
      },
      error: (error) => {
        this.lifecycleBusy.set(false);
        this.sessionError.set(this.mapSessionMutationError(error));
      }
    });
  }

  private applySessionDetail(detail: InterviewPrepSessionDetail, eTag: string): void {
    this.sessionDetail.set(detail);
    this.sessionEtag.set(eTag || detail.eTag);
    this.sessionId.set(detail.id);
  }

  private mapJobOptions(
    jobs: readonly { id: string; payload: { jobDetails?: { jobTitle?: string | null; companyName?: string | null } | null } }[]
  ): readonly InterviewPrepJobOption[] {
    return jobs.map((job) => {
      const title = job.payload?.jobDetails?.jobTitle ?? 'Saved job';
      const company = job.payload?.jobDetails?.companyName;
      const label = company ? `${title} · ${company}` : title;
      return { id: job.id, label };
    });
  }

  private mapSessionMutationError(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 409) {
      return 'This session changed elsewhere. Refresh to continue.';
    }
    return this.readError(error, 'Something went wrong. Try again.');
  }

  private readError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const body = error.error as { message?: string } | null;
      if (body?.message) {
        return body.message;
      }
      if (error.status === 0) {
        return 'Network error — is the API running?';
      }
    }
    return fallback;
  }
}

/** Stage statuses that still accept interview turns (mirrors GetActiveInterviewStage). */
function isActiveInterviewStageStatus(status: string): boolean {
  return (
    status === 'opening' ||
    status === 'warmUp' ||
    status === 'coreAssessment' ||
    status === 'candidateQuestions' ||
    status === 'closing' ||
    status === 'assessmentPending'
  );
}
