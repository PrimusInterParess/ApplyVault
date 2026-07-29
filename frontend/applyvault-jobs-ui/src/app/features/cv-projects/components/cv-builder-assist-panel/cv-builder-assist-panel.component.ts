import { Component, inject, input, output } from '@angular/core';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { CvStructuredFacade } from '../../data-access/cv-structured.facade';
import { CvImprovementSuggestion, CvStructuredSection } from '../../models/cv-structured.model';

@Component({
  selector: 'app-cv-builder-assist-panel',
  standalone: true,
  templateUrl: './cv-builder-assist-panel.component.html',
  styleUrl: './cv-builder-assist-panel.component.scss'
})
export class CvBuilderAssistPanelComponent {
  protected readonly cvStructured = inject(CvStructuredFacade);

  readonly open = input(false);
  readonly sections = input<readonly CvStructuredSection[]>([]);
  readonly aiUpdateSectionIds = input<readonly string[]>([]);
  readonly aiUpdateInstructions = input('');
  readonly selectedSuggestionIds = input<readonly string[]>([]);
  readonly suggestions = input<readonly CvImprovementSuggestion[]>([]);
  readonly disabled = input(false);

  readonly closePanel = output<void>();
  readonly aiInstructionsChange = output<string>();
  readonly toggleAiSection = output<string>();
  readonly updateWithAi = output<void>();
  readonly generateSuggestions = output<void>();
  readonly toggleSuggestion = output<string>();
  readonly applySuggestions = output<void>();

  protected onInstructionsInput(event: Event): void {
    this.aiInstructionsChange.emit(readInputValue(event));
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
}
