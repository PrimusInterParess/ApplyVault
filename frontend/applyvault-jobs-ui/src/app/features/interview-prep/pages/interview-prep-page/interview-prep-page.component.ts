import { CommonModule } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  ViewChild,
  computed,
  effect,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { InterviewPrepFacade } from '../../data-access/interview-prep.facade';
import {
  INTERVIEW_PREP_HIRING_MARKETS,
  INTERVIEW_PREP_INTERVIEWER_PROFILES,
  INTERVIEW_PREP_LANGUAGE_MIXES,
  INTERVIEW_PREP_MODES,
  INTERVIEW_PREP_SCORECARD_DIMENSION_LABELS,
  InterviewPrepHiringMarket,
  InterviewPrepHiringMarketOption,
  InterviewPrepInterviewerProfile,
  InterviewPrepInterviewerProfileOption,
  InterviewPrepLanguageMix,
  InterviewPrepLanguageOption,
  InterviewPrepMode,
  InterviewPrepModeOption,
  InterviewPrepScorecardDimensionId,
  InterviewPrepSessionSummary
} from '../../models/interview-prep.model';

type InterviewPrepHelpKey =
  | `mode:${InterviewPrepMode}`
  | `language:${InterviewPrepLanguageMix}`
  | `hiring:${InterviewPrepHiringMarket}`
  | `profile:${InterviewPrepInterviewerProfile}`;

interface InterviewPrepStageItem {
  readonly id: string;
  readonly label: string;
}

@Component({
  selector: 'app-interview-prep-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './interview-prep-page.component.html',
  styleUrl: './interview-prep-page.component.scss'
})
export class InterviewPrepPageComponent implements OnInit {
  protected readonly facade = inject(InterviewPrepFacade);
  protected readonly modes = INTERVIEW_PREP_MODES;
  protected readonly languageMixes = INTERVIEW_PREP_LANGUAGE_MIXES;
  protected readonly hiringMarkets = INTERVIEW_PREP_HIRING_MARKETS;
  protected readonly interviewerProfiles = INTERVIEW_PREP_INTERVIEWER_PROFILES;
  protected readonly readInputValue = readInputValue;

  /** While set, hide that chip's hover/focus popover until the pointer leaves. */
  protected readonly suppressedHelpKey = signal<InterviewPrepHelpKey | null>(null);

  protected readonly selectedMode = computed(
    (): InterviewPrepModeOption | undefined =>
      this.modes.find((mode) => mode.id === this.facade.mode())
  );

  protected readonly selectedLanguageMix = computed(
    (): InterviewPrepLanguageOption | undefined =>
      this.languageMixes.find((option) => option.id === this.facade.languageMix())
  );

  protected readonly selectedHiringMarket = computed(
    (): InterviewPrepHiringMarketOption | undefined =>
      this.hiringMarkets.find((option) => option.id === this.facade.hiringMarket())
  );

  protected readonly selectedInterviewerProfile = computed(
    (): InterviewPrepInterviewerProfileOption | undefined =>
      this.interviewerProfiles.find((option) => option.id === this.facade.interviewerProfile())
  );

  protected readonly agendaStageItems = computed(
    (): readonly InterviewPrepStageItem[] =>
      this.stageItemsForMode(this.facade.mode(), this.facade.interviewerProfile())
  );

  protected readonly currentStageIndex = computed(() => {
    const current = this.facade.currentAgendaStep();
    const index = this.agendaStageItems().findIndex((stage) => stage.id === current);
    return index >= 0 ? index : 0;
  });

  protected readonly stageProgressLabel = computed(() => {
    const total = this.agendaStageItems().length;
    return `Step ${Math.min(this.currentStageIndex() + 1, total)} of ${total}`;
  });

  protected readonly currentStageLabel = computed(() =>
    this.agendaStepLabel(this.facade.currentAgendaStep())
  );

  protected readonly interviewMoveTone = computed(() => {
    const move = this.facade.latestInterviewMove();
    const tones: Record<string, string> = {
      ask_new_question: 'The interviewer is opening a new topic.',
      probe_evidence: 'The interviewer is probing for concrete evidence.',
      clarify_ambiguity: 'The interviewer needs a clearer answer.',
      challenge_claim: 'The interviewer is challenging weak or unsupported claims.',
      transition_topic: 'The interviewer is moving to the next topic.',
      close_round: 'The interviewer is closing the round.'
    };

    return move ? tones[move] ?? 'The interviewer is steering the conversation.' : 'Answer naturally.';
  });

  @ViewChild('messagesList') private messagesList?: ElementRef<HTMLElement>;
  @ViewChild('composerInput') private composerInput?: ElementRef<HTMLTextAreaElement>;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    effect(() => {
      const messageCount = this.facade.messages().length;
      const sending = this.facade.sending();

      if (messageCount === 0 && !sending) {
        return;
      }

      setTimeout(() => this.scrollMessagesToEnd());
    });
  }

  ngOnInit(): void {
    this.facade.loadCvGate();
    this.facade.loadOwnedJobs();
    this.facade.loadHistory();

    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const jobId = params.get('jobId');
      this.facade.setScrapeResultIdFromJobId(jobId);
    });
  }

  protected selectMode(mode: InterviewPrepMode, event: Event): void {
    this.facade.setMode(mode);
    this.dismissChipHelp(this.modeHelpKey(mode), event);
  }

  protected selectLanguageMix(languageMix: InterviewPrepLanguageMix, event: Event): void {
    this.facade.setLanguageMix(languageMix);
    this.dismissChipHelp(this.languageHelpKey(languageMix), event);
  }

  protected selectHiringMarket(hiringMarket: InterviewPrepHiringMarket, event: Event): void {
    this.facade.setHiringMarket(hiringMarket);
    this.dismissChipHelp(this.hiringHelpKey(hiringMarket), event);
  }

  protected selectInterviewerProfile(
    interviewerProfile: InterviewPrepInterviewerProfile,
    event: Event
  ): void {
    this.facade.setInterviewerProfile(interviewerProfile);
    this.dismissChipHelp(this.profileHelpKey(interviewerProfile), event);
  }

  protected modeHelpKey(mode: InterviewPrepMode): InterviewPrepHelpKey {
    return `mode:${mode}`;
  }

  protected languageHelpKey(languageMix: InterviewPrepLanguageMix): InterviewPrepHelpKey {
    return `language:${languageMix}`;
  }

  protected hiringHelpKey(hiringMarket: InterviewPrepHiringMarket): InterviewPrepHelpKey {
    return `hiring:${hiringMarket}`;
  }

  protected profileHelpKey(
    interviewerProfile: InterviewPrepInterviewerProfile
  ): InterviewPrepHelpKey {
    return `profile:${interviewerProfile}`;
  }

  protected isHelpSuppressed(key: InterviewPrepHelpKey): boolean {
    return this.suppressedHelpKey() === key;
  }

  protected onChipHelpMouseLeave(key: InterviewPrepHelpKey): void {
    if (this.suppressedHelpKey() === key) {
      this.suppressedHelpKey.set(null);
    }
  }

  onJobSelect(rawValue: string): void {
    const jobId = rawValue.trim() || null;
    this.facade.selectOwnedJob(jobId);
    this.syncJobIdQuery(jobId);
  }

  continueAsGeneralPrep(): void {
    this.facade.clearJobLink();
    this.syncJobIdQuery(null);
  }

  protected dimensionLabel(id: string): string {
    return (
      INTERVIEW_PREP_SCORECARD_DIMENSION_LABELS[id as InterviewPrepScorecardDimensionId] ??
      id.replace(/_/g, ' ')
    );
  }

  protected modeLabel(modeId: string): string {
    return this.modes.find((mode) => mode.id === modeId)?.label ?? modeId.replace(/_/g, ' ');
  }

  protected statusLabel(status: string): string {
    if (status === 'completed') {
      return 'Completed';
    }
    if (status === 'in_progress') {
      return 'In progress';
    }
    return status.replace(/_/g, ' ');
  }

  protected agendaStepLabel(step: string): string {
    const labels: Record<string, string> = {
      opening: 'Opening',
      motivation_fit: 'Motivation and fit',
      cv_walkthrough: 'CV walkthrough',
      behavior_story: 'Behavioral story',
      evidence_probe: 'Evidence probe',
      case_setup: 'Scenario setup',
      approach_tradeoffs: 'Approach and trade-offs',
      process_map: 'Process map',
      failure_modes: 'Failure modes',
      phrasing_practice: 'Phrasing practice',
      rephrase_probe: 'Rephrase probe',
      role_depth: 'Role depth',
      scenario_probe: 'Scenario probe',
      challenge_claims: 'Challenge claims',
      candidate_questions: 'Candidate questions',
      debrief: 'Debrief'
    };

    return labels[step] ?? step.replace(/_/g, ' ');
  }

  protected interviewMoveLabel(move: string | null): string | null {
    if (!move) {
      return null;
    }

    const labels: Record<string, string> = {
      ask_new_question: 'New question',
      probe_evidence: 'Evidence probe',
      clarify_ambiguity: 'Clarification',
      challenge_claim: 'Challenge',
      transition_topic: 'Topic transition',
      close_round: 'Closing'
    };

    return labels[move] ?? move.replace(/_/g, ' ');
  }

  protected stageItemState(index: number): 'complete' | 'current' | 'upcoming' {
    const current = this.currentStageIndex();
    if (index < current) {
      return 'complete';
    }

    return index === current ? 'current' : 'upcoming';
  }

  private stageItemsForMode(
    mode: InterviewPrepMode,
    interviewerProfile: InterviewPrepInterviewerProfile
  ): readonly InterviewPrepStageItem[] {
    const opening: InterviewPrepStageItem = { id: 'opening', label: 'Opening' };
    const candidateQuestions: InterviewPrepStageItem = {
      id: 'candidate_questions',
      label: 'Candidate questions'
    };
    const debrief: InterviewPrepStageItem = { id: 'debrief', label: 'Debrief' };

    const base = (() => {
      switch (mode) {
        case 'screening':
          return [
            opening,
            { id: 'motivation_fit', label: 'Motivation' },
            { id: 'cv_walkthrough', label: 'CV walkthrough' },
            candidateQuestions,
            debrief
          ];
        case 'behavioral':
          return [
            opening,
            { id: 'behavior_story', label: 'Behavioral story' },
            { id: 'evidence_probe', label: 'Evidence probe' },
            candidateQuestions,
            debrief
          ];
        case 'problem_solving':
          return [
            opening,
            { id: 'case_setup', label: 'Scenario' },
            { id: 'approach_tradeoffs', label: 'Trade-offs' },
            candidateQuestions,
            debrief
          ];
        case 'process_systems':
          return [
            opening,
            { id: 'process_map', label: 'Process map' },
            { id: 'failure_modes', label: 'Failure modes' },
            candidateQuestions,
            debrief
          ];
        case 'language_practice':
          return [
            opening,
            { id: 'phrasing_practice', label: 'Phrasing' },
            { id: 'rephrase_probe', label: 'Rephrase probe' },
            candidateQuestions,
            debrief
          ];
        case 'full_loop':
          return [
            opening,
            { id: 'motivation_fit', label: 'Motivation' },
            { id: 'behavior_story', label: 'Behavioral' },
            { id: 'role_depth', label: 'Role depth' },
            { id: 'scenario_probe', label: 'Scenario' },
            candidateQuestions,
            debrief
          ];
        default:
          return [
            opening,
            { id: 'role_depth', label: 'Role depth' },
            { id: 'evidence_probe', label: 'Evidence probe' },
            candidateQuestions,
            debrief
          ];
      }
    })();

    if (interviewerProfile !== 'bar_raiser') {
      return base;
    }

    const insertAt = Math.max(1, base.length - 2);
    return [
      ...base.slice(0, insertAt),
      { id: 'challenge_claims', label: 'Challenge' },
      ...base.slice(insertAt)
    ];
  }

  protected historyJobLabel(item: InterviewPrepSessionSummary): string | null {
    const title = item.jobTitle?.trim();
    if (!title) {
      return null;
    }
    const company = item.companyName?.trim();
    return company ? `${company} · ${title}` : title;
  }

  protected onComposerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.facade.sendDraft();
    }
  }

  protected openHistorySession(sessionId: string): void {
    if (this.facade.sending() || this.facade.deletingSessionId()) {
      return;
    }

    this.facade.openSession(sessionId);
  }

  protected confirmDeleteSession(item: InterviewPrepSessionSummary, event: Event): void {
    event.stopPropagation();
    event.preventDefault();

    if (this.facade.deletingSessionId()) {
      return;
    }

    const label = this.historyJobLabel(item) ?? this.modeLabel(item.mode);
    const confirmed = window.confirm(
      `Delete this practice session (${label})? This cannot be undone.`
    );
    if (!confirmed) {
      return;
    }

    this.facade.deleteSession(item.id);
  }

  protected confirmNewRound(): void {
    if (this.facade.sessionStarted() || this.facade.messages().length > 0) {
      const confirmed = window.confirm(
        this.facade.isReadOnly()
          ? 'Return to setup? You can reopen this session from history.'
          : 'Start a new round? Your current session stays in history; this view returns to setup.'
      );
      if (!confirmed) {
        return;
      }
    }

    this.facade.resetSession();
  }

  protected insertFollowUp(text: string): void {
    this.facade.insertFollowUp(text);
    setTimeout(() => this.composerInput?.nativeElement.focus());
  }

  private dismissChipHelp(key: InterviewPrepHelpKey, event: Event): void {
    this.suppressedHelpKey.set(key);

    const target = event.currentTarget;
    if (target instanceof HTMLElement) {
      target.blur();
    }
  }

  private scrollMessagesToEnd(): void {
    const list = this.messagesList?.nativeElement;
    if (!list) {
      return;
    }

    list.scrollTop = list.scrollHeight;
  }

  private syncJobIdQuery(jobId: string | null): void {
    const current = this.route.snapshot.queryParamMap.get('jobId');

    if (jobId === current || (!jobId && !current)) {
      return;
    }

    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: jobId ? { jobId } : { jobId: null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }
}
