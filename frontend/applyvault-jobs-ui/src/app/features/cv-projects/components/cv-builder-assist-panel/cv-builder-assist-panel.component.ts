import { Component, ElementRef, input, output, viewChild } from '@angular/core';

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

@Component({
  selector: 'app-cv-builder-assist-panel',
  standalone: true,
  templateUrl: './cv-builder-assist-panel.component.html',
  styleUrl: './cv-builder-assist-panel.component.scss'
})
export class CvBuilderAssistPanelComponent {
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

  private readonly instructionsField =
    viewChild<ElementRef<HTMLTextAreaElement>>('aiInstructionsField');

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

    this.useFindingInAssist.emit(finding);
    queueMicrotask(() => this.focusInstructionsField());
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

  protected compareSectionIds(proposal: CvUpdateProposal): string[] {
    return resolveUpdateProposalCompareSectionIds(
      proposal.focusSectionIds,
      proposal.proposedSections
    );
  }

  protected sectionCompareHeading(sectionId: string, proposal: CvUpdateProposal): string {
    const proposed = proposal.proposedSections.find((section) => section.id === sectionId);
    const current = this.sections().find((section) => section.id === sectionId);
    const section = proposed ?? current;
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
