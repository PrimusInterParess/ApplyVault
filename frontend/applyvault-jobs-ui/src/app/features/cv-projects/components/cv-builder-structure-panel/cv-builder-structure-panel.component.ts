import { Component, input, output } from '@angular/core';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { CvSectionType, CvStructuredSection } from '../../models/cv-structured.model';

@Component({
  selector: 'app-cv-builder-structure-panel',
  standalone: true,
  templateUrl: './cv-builder-structure-panel.component.html',
  styleUrl: './cv-builder-structure-panel.component.scss'
})
export class CvBuilderStructurePanelComponent {
  readonly open = input(false);
  readonly sections = input<readonly CvStructuredSection[]>([]);
  readonly structureSectionTypes = input<readonly CvSectionType[]>([]);
  readonly pendingAddSectionType = input<CvSectionType>('Experience');
  /** When false, section mutations are disabled (shared editBusy / canMutateStructured). */
  readonly canMutate = input(true);

  readonly closePanel = output<void>();
  readonly pendingAddSectionTypeChange = output<CvSectionType>();
  readonly addSection = output<void>();
  readonly moveSection = output<{ sectionId: string; direction: -1 | 1 }>();
  readonly removeSection = output<string>();

  protected onPendingAddSectionTypeChange(event: Event): void {
    const value = readInputValue(event) as CvSectionType;

    if (this.structureSectionTypes().includes(value)) {
      this.pendingAddSectionTypeChange.emit(value);
    }
  }

  protected canMoveSectionUp(sectionId: string): boolean {
    return this.sections().findIndex((section) => section.id === sectionId) > 0;
  }

  protected canMoveSectionDown(sectionId: string): boolean {
    const items = this.sections();
    const index = items.findIndex((section) => section.id === sectionId);
    return index >= 0 && index < items.length - 1;
  }
}
