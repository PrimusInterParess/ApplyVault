import { Component, computed, effect, input, model, output, signal } from '@angular/core';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { SafeHtmlPipe } from '../../../../core/html/safe-html.pipe';
import {
  CvSectionType,
  CvStructuredEntry,
  CvStructuredSection
} from '../../models/cv-structured.model';
import {
  parseSectionMarkdown,
  sectionToMarkdown
} from '../../utils/cv-structured-text.util';
import { renderInlineMarkdown, renderMarkdown } from '../../utils/markdown.util';

@Component({
  selector: 'app-cv-structured-section-panel',
  standalone: true,
  imports: [SafeHtmlPipe],
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
  readonly draft = model<CvStructuredSection | null>(null);

  readonly edit = output<void>();
  readonly cancel = output<void>();
  readonly save = output<void>();
  public readonly includeInAiUpdate = output<void>();

  protected readonly renderMarkdown = renderMarkdown;
  protected readonly renderInlineMarkdown = renderInlineMarkdown;

  protected readonly editorText = signal('');

  protected readonly activeSection = computed(() =>
    this.editing() && this.draft() ? this.draft()! : this.section()
  );

  private editingActive = false;

  constructor() {
    effect(() => {
      const editing = this.editing();
      const draft = this.draft();

      if (!editing) {
        this.editingActive = false;
        this.editorText.set('');
        return;
      }

      if (!this.editingActive && draft) {
        this.editorText.set(sectionToMarkdown(draft));
      }

      this.editingActive = true;
    });
  }

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

  protected updateEditorText(event: Event): void {
    const value = readInputValue(event);
    const currentDraft = this.draft();

    this.editorText.set(value);

    if (!currentDraft || !this.editing() || this.saving()) {
      return;
    }

    this.draft.set(parseSectionMarkdown(value, currentDraft));
  }
}
