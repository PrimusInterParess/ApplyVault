import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild
} from '@angular/core';

import { readInputValue } from '../../../../core/dom/input-value.util';
import {
  CvImprovementSuggestion,
  CvQualityEvaluation,
  CvQualityEvaluationFinding,
  CvStructuredSection,
  CvSummaryProposal,
  CvUpdateProposal
} from '../../models/cv-structured.model';
import {
  formatSectionForAssistCompare,
  resolveUpdateProposalCompareSectionIds
} from '../../utils/cv-update-proposal-compare.util';

export type CvAssistToolTab = 'summary' | 'update' | 'suggestions' | 'evaluate';

interface CvAssistToolTabDef {
  readonly id: CvAssistToolTab;
  readonly shortLabel: string;
  readonly panelTitle: string;
}

@Component({
  selector: 'app-cv-builder-assist-panel',
  standalone: true,
  templateUrl: './cv-builder-assist-panel.component.html',
  styleUrl: './cv-builder-assist-panel.component.scss'
})
export class CvBuilderAssistPanelComponent {
  private readonly changeDetector = inject(ChangeDetectorRef);

  readonly open = input(false);
  readonly sections = input<readonly CvStructuredSection[]>([]);
  readonly aiUpdateSectionIds = input<readonly string[]>([]);
  readonly aiUpdateInstructions = input('');
  readonly summaryProposeInstructions = input('');
  readonly selectedSuggestionIds = input<readonly string[]>([]);
  readonly suggestions = input<readonly CvImprovementSuggestion[]>([]);
  readonly evaluation = input<CvQualityEvaluation | null>(null);
  readonly summaryProposal = input<CvSummaryProposal | null>(null);
  readonly updateProposal = input<CvUpdateProposal | null>(null);
  readonly disabled = input(false);
  readonly proposingUpdate = input(false);
  readonly generatingSuggestions = input(false);
  readonly evaluating = input(false);
  readonly proposing = input(false);
  readonly aiUpdateError = input<string | null>(null);
  readonly suggestionError = input<string | null>(null);
  readonly evaluationError = input<string | null>(null);
  readonly summaryProposalError = input<string | null>(null);

  readonly closePanel = output<void>();
  readonly aiInstructionsChange = output<string>();
  readonly summaryProposeInstructionsChange = output<string>();
  readonly toggleAiSection = output<string>();
  readonly proposeUpdate = output<void>();
  readonly approveUpdateProposal = output<void>();
  readonly discardUpdateProposal = output<void>();
  readonly generateSuggestions = output<void>();
  readonly toggleSuggestion = output<string>();
  readonly applySuggestions = output<void>();
  readonly evaluateQuality = output<void>();
  readonly useFindingInAssist = output<CvQualityEvaluationFinding>();
  readonly proposeSummary = output<void>();
  readonly approveSummaryProposal = output<void>();
  readonly discardSummaryProposal = output<void>();

  protected readonly toolTabs: readonly CvAssistToolTabDef[] = [
    { id: 'summary', shortLabel: 'Summary', panelTitle: 'Regenerate summary' },
    { id: 'update', shortLabel: 'Update', panelTitle: 'Update with instructions' },
    { id: 'suggestions', shortLabel: 'Suggestions', panelTitle: 'Suggestions' },
    { id: 'evaluate', shortLabel: 'Evaluate', panelTitle: 'Evaluate CV' }
  ];

  protected readonly activeToolTab = signal<CvAssistToolTab>('summary');

  private readonly instructionsField =
    viewChild<ElementRef<HTMLTextAreaElement>>('aiInstructionsField');

  protected selectToolTab(tab: CvAssistToolTab): void {
    this.activeToolTab.set(tab);
  }

  protected onToolTabKeydown(event: KeyboardEvent, tab: CvAssistToolTab): void {
    const tabs = this.toolTabs;
    const currentIndex = tabs.findIndex((entry) => entry.id === tab);
    if (currentIndex < 0) {
      return;
    }

    let nextIndex: number | null = null;
    switch (event.key) {
      case 'ArrowRight':
      case 'ArrowDown':
        nextIndex = (currentIndex + 1) % tabs.length;
        break;
      case 'ArrowLeft':
      case 'ArrowUp':
        nextIndex = (currentIndex - 1 + tabs.length) % tabs.length;
        break;
      case 'Home':
        nextIndex = 0;
        break;
      case 'End':
        nextIndex = tabs.length - 1;
        break;
      default:
        return;
    }

    event.preventDefault();
    const nextTab = tabs[nextIndex];
    this.activeToolTab.set(nextTab.id);
    queueMicrotask(() => {
      const button = document.getElementById(`cv-assist-tab-${nextTab.id}`);
      button?.focus();
    });
  }

  protected tabHasPending(tab: CvAssistToolTab): boolean {
    switch (tab) {
      case 'summary':
        return this.summaryProposal() !== null;
      case 'update':
        return this.updateProposal() !== null;
      case 'evaluate':
        return this.evaluation() !== null;
      default:
        return false;
    }
  }

  protected tabAriaLabel(tab: CvAssistToolTabDef): string {
    if (!this.tabHasPending(tab.id)) {
      return tab.shortLabel;
    }

    switch (tab.id) {
      case 'summary':
        return 'Summary, proposal pending';
      case 'update':
        return 'Update, proposal pending';
      case 'evaluate':
        return 'Evaluate, report present';
      default:
        return tab.shortLabel;
    }
  }

  protected onInstructionsInput(event: Event): void {
    this.aiInstructionsChange.emit(readInputValue(event));
  }

  protected onSummaryProposeInstructionsInput(event: Event): void {
    this.summaryProposeInstructionsChange.emit(readInputValue(event));
  }

  protected onUseFindingInAssist(finding: CvQualityEvaluationFinding): void {
    if (this.disabled()) {
      return;
    }

    this.activeToolTab.set('update');
    this.useFindingInAssist.emit(finding);
    // Flush [hidden] so the Update instructions field is focusable.
    this.changeDetector.detectChanges();
    this.focusInstructionsField();
  }

  /** Scroll/focus Update-with-instructions after a finding is copied (D5 polish). */
  focusInstructionsField(): void {
    const field = this.instructionsField()?.nativeElement;
    if (!field) {
      return;
    }

    field.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    field.focus({ preventScroll: true });
  }

  protected isAiSectionSelected(sectionId: string): boolean {
    return this.aiUpdateSectionIds().includes(sectionId);
  }

  protected isSuggestionSelected(suggestionId: string): boolean {
    return this.selectedSuggestionIds().includes(suggestionId);
  }

  protected aiSectionLabel(section: CvStructuredSection): string {
    return section.heading.trim() || section.sectionType;
  }

  /** Resolve API sectionId to a display title from the current CV; never return the raw id. */
  protected sectionTitleById(sectionId: string | null | undefined): string | null {
    if (!sectionId) {
      return null;
    }

    const section = this.sections().find((entry) => entry.id === sectionId);
    return section ? this.aiSectionLabel(section) : null;
  }

  protected compareSectionIds(proposal: CvUpdateProposal): string[] {
    return resolveUpdateProposalCompareSectionIds(
      proposal.focusSectionIds,
      proposal.proposedSections,
      this.sections()
    );
  }

  protected sectionCompareHeading(sectionId: string, proposal: CvUpdateProposal): string {
    const current = this.sections().find((section) => section.id === sectionId);
    const proposed = proposal.proposedSections.find((section) => section.id === sectionId);
    const section = current ?? proposed;
    return section ? this.aiSectionLabel(section) : 'Section';
  }

  protected currentSectionText(sectionId: string): string {
    return formatSectionForAssistCompare(
      this.sections().find((section) => section.id === sectionId)
    );
  }

  protected proposedSectionText(sectionId: string, proposal: CvUpdateProposal): string {
    return formatSectionForAssistCompare(
      proposal.proposedSections.find((section) => section.id === sectionId)
    );
  }

  protected dimensionLabel(dimensionId: string): string {
    switch (dimensionId) {
      case 'content':
        return 'Content';
      case 'structure':
        return 'Structure';
      case 'format':
        return 'Format';
      default:
        return dimensionId;
    }
  }

  protected severityLabel(severity: string): string {
    switch (severity) {
      case 'critical':
        return 'Critical';
      case 'warning':
        return 'Warning';
      case 'info':
        return 'Info';
      default:
        return severity;
    }
  }
}
