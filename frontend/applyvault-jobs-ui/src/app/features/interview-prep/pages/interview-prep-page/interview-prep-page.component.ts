import { CommonModule } from '@angular/common';
import {
  afterNextRender,
  Component,
  effect,
  ElementRef,
  inject,
  Injector,
  OnInit,
  signal,
  viewChild
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { InterviewPrepFacade } from '../../data-access/interview-prep.facade';
import {
  INTERVIEW_PREP_BRIEF_TOPIC_GAPS,
  INTERVIEW_PREP_EXPERIENCE_TYPES,
  INTERVIEW_PREP_FOCUS_NOTE_MAX_LENGTH,
  INTERVIEW_PREP_LANGUAGES,
  INTERVIEW_PREP_MARKETS,
  INTERVIEW_PREP_MODES,
  INTERVIEW_PREP_PERSONAS,
  InterviewPrepBriefOutdatedReason,
  InterviewPrepBriefTopicGap,
  InterviewPrepExperienceType,
  InterviewPrepLanguage,
  InterviewPrepMarket,
  InterviewPrepMode,
  InterviewPrepPageSurface,
  InterviewPrepPersona,
  InterviewPrepSessionStatus,
  InterviewPrepSessionSummary,
  InterviewPrepStudyBrief,
  InterviewPrepTurn
} from '../../models/interview-prep.model';

@Component({
  selector: 'app-interview-prep-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './interview-prep-page.component.html',
  styleUrl: './interview-prep-page.component.scss'
})
export class InterviewPrepPageComponent implements OnInit {
  protected readonly facade = inject(InterviewPrepFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly injector = inject(Injector);

  private readonly chatScroll = viewChild<ElementRef<HTMLElement>>('chatScroll');

  protected readonly answerDraft = signal('');
  protected readonly retryDraft = signal('');

  protected readonly modes = INTERVIEW_PREP_MODES;
  protected readonly personas = INTERVIEW_PREP_PERSONAS;
  protected readonly languages = INTERVIEW_PREP_LANGUAGES;
  protected readonly markets = INTERVIEW_PREP_MARKETS;
  protected readonly experienceTypes = INTERVIEW_PREP_EXPERIENCE_TYPES;
  protected readonly topicGaps = INTERVIEW_PREP_BRIEF_TOPIC_GAPS;
  protected readonly focusNoteMaxLength = INTERVIEW_PREP_FOCUS_NOTE_MAX_LENGTH;

  constructor() {
    effect(() => {
      const turnCount = this.facade.chatTurns().length;
      if (turnCount === 0) {
        return;
      }
      // Scroll inside the fixed chat pane only — never grow the page.
      afterNextRender(
        () => {
          const el = this.chatScroll()?.nativeElement;
          if (!el) {
            return;
          }
          const reduceMotion =
            typeof matchMedia === 'function' &&
            matchMedia('(prefers-reduced-motion: reduce)').matches;
          el.scrollTo({
            top: el.scrollHeight,
            behavior: reduceMotion ? 'auto' : 'smooth'
          });
        },
        { injector: this.injector }
      );
    });
  }

  ngOnInit(): void {
    this.facade.loadCvGate();
    this.facade.loadJobOptions();
    this.facade.loadHistory();

    const surface = this.route.snapshot.queryParamMap.get('surface');
    this.facade.applySurfaceFromQuery(surface);

    const jobId = this.route.snapshot.queryParamMap.get('jobId');
    this.facade.applyJobIdFromQuery(jobId);

    if (this.facade.surface() === 'study' && !jobId) {
      this.facade.loadStudyBriefs();
    }

    const sessionId = this.route.snapshot.queryParamMap.get('sessionId');
    if (sessionId && this.facade.surface() === 'practice') {
      this.facade.loadSession(sessionId);
    }
  }

  protected selectSurface(surface: InterviewPrepPageSurface, event: Event): void {
    event.preventDefault();
    this.facade.setSurface(surface);
  }

  protected selectMode(mode: InterviewPrepMode, event: Event): void {
    event.preventDefault();
    this.facade.setMode(mode);
  }

  protected selectPersona(persona: InterviewPrepPersona, event: Event): void {
    event.preventDefault();
    this.facade.setPersona(persona);
  }

  protected isPersonaEnabled(persona: InterviewPrepPersona): boolean {
    return this.facade.availablePersonas().includes(persona);
  }

  protected selectLanguage(language: InterviewPrepLanguage, event: Event): void {
    event.preventDefault();
    this.facade.setLanguage(language);
  }

  protected selectMarket(market: InterviewPrepMarket, event: Event): void {
    event.preventDefault();
    this.facade.setMarket(market);
  }

  protected selectExperienceType(type: InterviewPrepExperienceType, event: Event): void {
    event.preventDefault();
    this.facade.setExperienceType(type);
  }

  protected onJobChange(value: string): void {
    this.facade.setSelectedJob(value || null);
  }

  protected submitAnswer(): void {
    const text = this.answerDraft();
    if (!text.trim()) {
      return;
    }
    this.facade.submitAnswer(text);
    this.answerDraft.set('');
  }

  protected openHistorySession(item: InterviewPrepSessionSummary, event?: Event): void {
    event?.preventDefault();
    this.facade.loadSession(item.id);
  }

  protected isHistoryActive(item: InterviewPrepSessionSummary): boolean {
    return this.facade.sessionId() === item.id;
  }

  protected openStudyBrief(item: InterviewPrepStudyBrief, event?: Event): void {
    event?.preventDefault();
    this.facade.selectStudyBrief(item);
  }

  protected isStudyBriefActive(item: InterviewPrepStudyBrief): boolean {
    return this.facade.selectedStudyBrief()?.id === item.id;
  }

  protected confirmDeleteStudyBrief(item: InterviewPrepStudyBrief, event: Event): void {
    event.preventDefault();
    if (window.confirm('Delete this study brief?')) {
      this.facade.deleteStudyBrief(item.id);
    }
  }

  protected studyBriefJobLabel(item: InterviewPrepStudyBrief): string {
    if (item.jobTitle && item.companyName) {
      return `${item.jobTitle} · ${item.companyName}`;
    }
    if (item.jobTitle || item.companyName) {
      return item.jobTitle ?? item.companyName ?? 'Saved job';
    }
    return 'CV only';
  }

  protected gapLabel(gap: InterviewPrepBriefTopicGap | string): string {
    return this.topicGaps.find((g) => g.id === gap)?.label ?? gap;
  }

  protected outdatedReasonLabel(reason: InterviewPrepBriefOutdatedReason | string): string {
    if (reason === 'structuredCvChanged') {
      return 'Structured CV changed since this brief was generated';
    }
    if (reason === 'boundJobMissing') {
      return 'Bound saved job is missing or deleted';
    }
    return reason;
  }

  protected sortedTopics(brief: InterviewPrepStudyBrief) {
    return [...brief.topics].sort((a, b) => a.priority - b.priority);
  }

  protected studyBusyLabel(): string {
    const busy = this.facade.studyBriefBusy();
    if (busy === 'generating') {
      return 'Generating study brief…';
    }
    if (busy === 'regenerating') {
      return 'Regenerating study brief…';
    }
    if (busy === 'deleting') {
      return 'Deleting…';
    }
    return '';
  }

  protected retryCoachingReview(candidateTurnId: string): void {
    this.facade.openCoachingReview(candidateTurnId);
  }

  protected confirmDeleteSession(item: InterviewPrepSessionSummary, event: Event): void {
    event.preventDefault();
    if (window.confirm('Delete this practice session?')) {
      this.facade.deleteHistorySession(item.id);
    }
  }

  protected turnRoleLabel(turn: InterviewPrepTurn): string {
    if (turn.role === 'interviewer') {
      return 'Interviewer';
    }
    if (turn.role === 'candidate') {
      return 'You';
    }
    if (turn.role === 'coach') {
      return 'Coach';
    }
    return 'System';
  }

  protected historyJobLabel(item: InterviewPrepSessionSummary): string | null {
    if (item.jobTitle && item.companyName) {
      return `${item.jobTitle} · ${item.companyName}`;
    }
    return item.jobTitle ?? item.companyName ?? null;
  }

  protected needsContinue(): boolean {
    const status = this.facade.sessionDetail()?.status;
    return status === 'created' || status === 'ready';
  }

  protected experienceDescription(): string {
    return (
      this.experienceTypes.find((t) => t.id === this.facade.experienceType())?.description ?? ''
    );
  }

  protected modeLabel(mode: InterviewPrepMode | string | null | undefined): string {
    return this.modes.find((m) => m.id === mode)?.label ?? mode ?? '';
  }

  protected personaLabel(persona: InterviewPrepPersona | string | null | undefined): string {
    return this.personas.find((p) => p.id === persona)?.label ?? persona ?? '';
  }

  protected sessionStatusLabel(status: InterviewPrepSessionStatus | string | null | undefined): string {
    const labels: Record<string, string> = {
      created: 'Created',
      preparing: 'Preparing',
      ready: 'Ready',
      inProgress: 'In progress',
      paused: 'Paused',
      completing: 'Completing',
      completed: 'Completed',
      cancelled: 'Cancelled',
      failed: 'Failed'
    };
    if (!status) {
      return '';
    }
    return labels[status] ?? status;
  }

  protected languageLabel(language: string | null | undefined): string {
    return this.languages.find((l) => l.id === language)?.label ?? language ?? '';
  }

  protected marketLabel(market: string | null | undefined): string {
    return this.markets.find((m) => m.id === market)?.label ?? market ?? '';
  }

  protected showTurnLanguage(turn: InterviewPrepTurn): boolean {
    if (!turn.language) {
      return false;
    }
    const sessionLang = this.facade.sessionDetail()?.language ?? this.facade.language();
    return turn.language !== sessionLang;
  }

  protected submitRetry(): void {
    this.facade.submitCoachingRetry(this.retryDraft());
    this.retryDraft.set('');
  }
}
