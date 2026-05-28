import { Component, computed, input, model, output } from '@angular/core';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { SafeHtmlPipe } from '../../../../core/html/safe-html.pipe';
import {
  CvSectionType,
  CvStructuredEntry,
  CvStructuredSection
} from '../../models/cv-structured.model';
import {
  addEntryToSection,
  entryHasContent,
  moveEntryInSection,
  removeEntryFromSection,
  updateEntryField,
  updateSectionHeading
} from '../../utils/cv-structured-draft.util';
import { entryBodySourceText } from '../../utils/cv-structured-edit-normalizer.util';
import { renderInlineMarkdown, renderMarkdown } from '../../utils/markdown.util';
import { CvStructuredEntryEditorComponent } from '../cv-structured-entry-editor/cv-structured-entry-editor.component';

@Component({
  selector: 'app-cv-structured-section-panel',
  standalone: true,
  imports: [SafeHtmlPipe, CvStructuredEntryEditorComponent],
  templateUrl: './cv-structured-section-panel.component.html',
  styleUrl: './cv-structured-section-panel.component.scss',
  host: {
    '[class.cv-section-host--contact]': 'isContactSection(activeSection())',
    '[class.cv-section-host--summary]': 'isSummarySection(activeSection().sectionType)'
  }
})
export class CvStructuredSectionPanelComponent {
  readonly section = input.required<CvStructuredSection>();
  readonly editing = input(false);
  readonly saving = input(false);
  readonly disabled = input(false);
  readonly saveError = input<string | null>(null);
  readonly canSave = input(false);
  public readonly aiUpdateSelected = input(false);
  public readonly suggestionSelected = input(false);
  public readonly showAiUpdateAction = input(false);
  readonly editFormKey = input(0);
  readonly draft = model<CvStructuredSection | null>(null);

  readonly edit = output<void>();
  readonly cancel = output<void>();
  readonly save = output<void>();
  public readonly includeInAiUpdate = output<void>();

  protected readonly renderMarkdown = renderMarkdown;
  protected readonly renderInlineMarkdown = renderInlineMarkdown;

  protected readonly activeSection = computed(() => {
    const draft = this.draft();

    if (this.editing() && draft && draft.id === this.section().id) {
      return draft;
    }

    return this.section();
  });

  protected readonly sortedEntries = computed(() =>
    [...this.activeSection().entries].sort((left, right) => left.sortOrder - right.sortOrder)
  );

  protected readonly hasEntries = computed(() => this.sortedEntries().length > 0);

  protected isSummarySection(sectionType: CvSectionType): boolean {
    return sectionType === 'Summary';
  }

  protected isSkillsSection(sectionType: CvSectionType): boolean {
    return sectionType === 'Skills';
  }

  protected isContactSection(section: CvStructuredSection): boolean {
    return section.heading.trim().toLowerCase() === 'contact';
  }

  protected entryMeta(entry: CvStructuredEntry): string {
    return [entry.subtitle?.trim(), entry.dateRange?.trim()].filter(Boolean).join(' · ');
  }

  protected techStackItems(entry: CvStructuredEntry): readonly string[] {
    const techStack = entry.techStack?.trim();

    if (!techStack) {
      return [];
    }

    return techStack
      .split(/[,;|]/)
      .map((item) => item.trim())
      .filter((item) => item.length > 0);
  }

  protected skillItems(entry: CvStructuredEntry): readonly string[] {
    const techItems = this.techStackItems(entry);

    if (techItems.length > 0) {
      return techItems;
    }

    return entry.bullets.map((bullet) => bullet.trim()).filter((bullet) => bullet.length > 0);
  }

  protected readonly entryBodySourceText = entryBodySourceText;

  protected updateHeading(event: Event): void {
    const draft = this.draft();

    if (!draft || draft.id !== this.section().id || !this.editing() || this.saving()) {
      return;
    }

    this.draft.set(updateSectionHeading(draft, readInputValue(event)));
  }

  protected updateEntry(entryId: string, patch: Partial<CvStructuredEntry>): void {
    const draft = this.draft();

    if (!draft || draft.id !== this.section().id || !this.editing() || this.saving()) {
      return;
    }

    this.draft.set(updateEntryField(draft, entryId, patch));
  }

  protected addEntry(): void {
    const draft = this.draft();

    if (!draft || !this.editing() || this.saving()) {
      return;
    }

    this.draft.set(addEntryToSection(draft));
  }

  protected removeEntry(entry: CvStructuredEntry): void {
    const draft = this.draft();

    if (!draft || !this.editing() || this.saving()) {
      return;
    }

    if (entryHasContent(entry) && !window.confirm('Remove this entry and its content?')) {
      return;
    }

    this.draft.set(removeEntryFromSection(draft, entry.id));
  }

  protected moveEntry(entryId: string, direction: 'up' | 'down'): void {
    const draft = this.draft();

    if (!draft || !this.editing() || this.saving()) {
      return;
    }

    const entries = [...draft.entries].sort((left, right) => left.sortOrder - right.sortOrder);
    const index = entries.findIndex((entry) => entry.id === entryId);

    if (index < 0) {
      return;
    }

    const targetIndex = direction === 'up' ? index - 1 : index + 1;

    if (targetIndex < 0 || targetIndex >= entries.length) {
      return;
    }

    this.draft.set(moveEntryInSection(draft, index, targetIndex));
  }
}
