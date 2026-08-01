import { Component, ElementRef, input, output, viewChild } from '@angular/core';

import { readInputValue } from '../../../../core/dom/input-value.util';
import {
  CvImprovementSuggestion,
  CvQualityEvaluation,
  CvQualityEvaluationFinding,
  CvStructuredSection
} from '../../models/cv-structured.model';

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
  readonly selectedSuggestionIds = input<readonly string[]>([]);
  readonly suggestions = input<readonly CvImprovementSuggestion[]>([]);
  readonly evaluation = input<CvQualityEvaluation | null>(null);
  readonly disabled = input(false);
  readonly updatingWithAi = input(false);
  readonly generatingSuggestions = input(false);
  readonly evaluating = input(false);
  readonly aiUpdateError = input<string | null>(null);
  readonly suggestionError = input<string | null>(null);
  readonly evaluationError = input<string | null>(null);

  readonly closePanel = output<void>();
  readonly aiInstructionsChange = output<string>();
  readonly toggleAiSection = output<string>();
  readonly updateWithAi = output<void>();
  readonly generateSuggestions = output<void>();
  readonly toggleSuggestion = output<string>();
  readonly applySuggestions = output<void>();
  readonly evaluateQuality = output<void>();
  readonly useFindingInAssist = output<CvQualityEvaluationFinding>();

  private readonly instructionsField =
    viewChild<ElementRef<HTMLTextAreaElement>>('aiInstructionsField');

  protected onInstructionsInput(event: Event): void {
    this.aiInstructionsChange.emit(readInputValue(event));
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
