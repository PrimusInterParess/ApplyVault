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
  INTERVIEW_PREP_LANGUAGE_MIXES,
  INTERVIEW_PREP_MODES,
  INTERVIEW_PREP_SCORECARD_DIMENSION_LABELS,
  InterviewPrepHiringMarket,
  InterviewPrepHiringMarketOption,
  InterviewPrepLanguageMix,
  InterviewPrepLanguageOption,
  InterviewPrepMode,
  InterviewPrepModeOption,
  InterviewPrepScorecardDimensionId
} from '../../models/interview-prep.model';

type InterviewPrepHelpKey =
  | `mode:${InterviewPrepMode}`
  | `language:${InterviewPrepLanguageMix}`
  | `hiring:${InterviewPrepHiringMarket}`;

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

  protected modeHelpKey(mode: InterviewPrepMode): InterviewPrepHelpKey {
    return `mode:${mode}`;
  }

  protected languageHelpKey(languageMix: InterviewPrepLanguageMix): InterviewPrepHelpKey {
    return `language:${languageMix}`;
  }

  protected hiringHelpKey(hiringMarket: InterviewPrepHiringMarket): InterviewPrepHelpKey {
    return `hiring:${hiringMarket}`;
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

  protected onComposerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.facade.sendDraft();
    }
  }

  protected confirmNewRound(): void {
    if (this.facade.messages().length > 0) {
      const confirmed = window.confirm(
        'Start a new round? This clears the current conversation in this tab.'
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
