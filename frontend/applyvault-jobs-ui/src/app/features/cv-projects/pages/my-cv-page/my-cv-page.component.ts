import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, effect, inject, signal, viewChild, ElementRef } from '@angular/core';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import { CvStructuredSectionPanelComponent } from '../../components/cv-structured-section-panel/cv-structured-section-panel.component';
import { CvDocumentFacade } from '../../data-access/cv-document.facade';
import { CvStructuredFacade } from '../../data-access/cv-structured.facade';
import { CvImprovementSuggestion, CvStructuredSection } from '../../models/cv-structured.model';
import {
  CV_EXPORT_TEMPLATES,
  DEFAULT_CV_EXPORT_TEMPLATE_ID,
  MAX_CV_EXPORT_TEMPLATE_ID
} from '../../models/cv-export-template.model';
import {
  cloneSectionForDraft,
  mergeSection,
  reorderSections,
  sectionEquals,
  sectionsAreEqual
} from '../../utils/cv-structured-draft.util';

@Component({
  selector: 'app-my-cv-page',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DragDropModule,
    SkeletonBlockComponent,
    CvStructuredSectionPanelComponent
  ],
  templateUrl: './my-cv-page.component.html',
  styleUrl: './my-cv-page.component.scss'
})
export class MyCvPageComponent {
  protected readonly cvDocument = inject(CvDocumentFacade);
  protected readonly cvStructured = inject(CvStructuredFacade);
  protected readonly cvExportTemplates = CV_EXPORT_TEMPLATES;
  protected readonly defaultCvExportTemplateId = DEFAULT_CV_EXPORT_TEMPLATE_ID;
  protected readonly deleteConfirmOpen = signal(false);
  protected readonly aiPanelOpen = signal(false);
  protected readonly suggestionsPanelOpen = signal(false);
  protected readonly editingSectionId = signal<string | null>(null);
  protected readonly sectionDraft = signal<CvStructuredSection | null>(null);
  protected readonly aiUpdateInstructions = signal('');
  protected readonly aiUpdateSectionIds = signal<string[]>([]);
  protected readonly selectedSuggestionIds = signal<string[]>([]);
  protected readonly sectionOrderDraft = signal<CvStructuredSection[] | null>(null);
  protected readonly cvFileInput = viewChild<ElementRef<HTMLInputElement>>('cvFileInput');

  protected readonly extractionStatus = computed(() => this.cvDocument.importSummary());

  protected readonly serverSections = computed(() => {
    const items = this.cvStructured.structured()?.sections ?? [];

    return [...items].sort((left, right) => left.sortOrder - right.sortOrder);
  });

  protected readonly sections = computed(() => {
    const draft = this.sectionOrderDraft();

    return draft ?? this.serverSections();
  });

  protected readonly hasPendingSectionReorder = computed(() => {
    const draft = this.sectionOrderDraft();

    if (!draft) {
      return false;
    }

    return !sectionsAreEqual(draft, this.serverSections());
  });

  protected readonly hasSections = computed(() => this.sections().length > 0);

  protected readonly canDownloadFormatted = computed(() => {
    const document = this.cvDocument.document();

    return (
      !!document?.hasStructuredContent &&
      this.hasSections() &&
      !this.cvDocument.loading() &&
      !this.cvStructured.loading() &&
      !this.cvDocument.downloadingFormatted()
    );
  });

  protected readonly canUpdateWithAi = computed(() =>
    this.cvDocument.hasDocument() &&
    this.hasSections() &&
    this.aiUpdateInstructions().trim().length > 0 &&
    !this.cvDocument.loading() &&
    !this.cvStructured.loading() &&
    !this.cvStructured.savingSectionId() &&
    !this.cvStructured.savingSectionOrder() &&
    !this.cvStructured.updatingWithAi() &&
    !this.cvStructured.generatingSuggestions() &&
    !this.editingSectionId() &&
    !this.hasPendingSectionReorder()
  );

  protected readonly canGenerateSuggestions = computed(() =>
    this.cvDocument.hasDocument() &&
    this.hasSections() &&
    !this.cvDocument.loading() &&
    !this.cvStructured.loading() &&
    !this.cvStructured.savingSectionId() &&
    !this.cvStructured.savingSectionOrder() &&
    !this.cvStructured.updatingWithAi() &&
    !this.cvStructured.generatingSuggestions() &&
    !this.editingSectionId() &&
    !this.hasPendingSectionReorder()
  );

  protected readonly canReorderSections = computed(() =>
    this.hasSections() &&
    !this.cvDocument.loading() &&
    !this.cvStructured.loading() &&
    !this.cvStructured.savingSectionId() &&
    !this.cvStructured.savingSectionOrder() &&
    !this.cvStructured.updatingWithAi() &&
    !this.cvStructured.generatingSuggestions() &&
    !this.editingSectionId()
  );

  protected readonly canSaveSectionOrder = computed(() =>
    this.hasPendingSectionReorder() &&
    !this.cvStructured.savingSectionOrder() &&
    !this.cvStructured.savingSectionId() &&
    !this.editingSectionId()
  );

  protected readonly selectedSuggestions = computed(() => {
    const selected = new Set(this.selectedSuggestionIds());

    return this.cvStructured.suggestions().filter((suggestion) => selected.has(suggestion.id));
  });

  protected readonly selectedSuggestionSectionIds = computed(() => {
    const sectionIds = new Set<string>();
    const sections = this.sections();

    for (const suggestion of this.selectedSuggestions()) {
      const resolved = this.resolveSuggestionSectionId(suggestion, sections);
      if (resolved) {
        sectionIds.add(resolved);
      }
    }

    return sectionIds;
  });

  protected readonly canApplySelectedSuggestions = computed(() =>
    this.selectedSuggestions().length > 0 &&
    !this.cvDocument.loading() &&
    !this.cvStructured.loading() &&
    !this.cvStructured.savingSectionId() &&
    !this.cvStructured.savingSectionOrder() &&
    !this.cvStructured.updatingWithAi() &&
    !this.cvStructured.generatingSuggestions() &&
    !this.editingSectionId() &&
    !this.hasPendingSectionReorder()
  );

  protected readonly canSaveSection = computed(() => {
    const sectionId = this.editingSectionId();
    const draft = this.sectionDraft();

    if (!sectionId || !draft) {
      return false;
    }

    const savedSection = this.sections().find((section) => section.id === sectionId);

    if (!savedSection) {
      return false;
    }

    return !sectionEquals(draft, savedSection);
  });

  protected readonly structuredReloadKey = computed(() => {
    const document = this.cvDocument.document();
    const importSummary = this.cvDocument.importSummary();

    return [
      document?.hasStructuredContent ?? false,
      document?.structuredImportedAt ?? '',
      importSummary?.sectionCount ?? 0,
      importSummary?.succeeded ?? false
    ].join('|');
  });

  private lastSavingSectionId: string | null = null;
  private wasSavingSectionOrder = false;
  private wasUpdatingWithAi = false;

  constructor() {
    effect(() => {
      this.structuredReloadKey();

      if (!this.cvDocument.loading() && this.cvDocument.hasDocument()) {
        this.cvStructured.load();
      }
    });

    effect(() => {
      this.structuredReloadKey();
      this.cancelSectionEdit();
      this.discardSectionOrder();
    });

    effect(() => {
      const savingSectionOrder = this.cvStructured.savingSectionOrder();
      const saveError = this.cvStructured.saveError();

      if (this.wasSavingSectionOrder && !savingSectionOrder && !saveError) {
        this.discardSectionOrder();
      }

      this.wasSavingSectionOrder = savingSectionOrder;
    });

    effect(() => {
      const savingSectionId = this.cvStructured.savingSectionId();
      const saveError = this.cvStructured.saveError();

      if (this.lastSavingSectionId && !savingSectionId && !saveError) {
        this.cancelSectionEdit();
      }

      this.lastSavingSectionId = savingSectionId;
    });

    effect(() => {
      const updatingWithAi = this.cvStructured.updatingWithAi();
      const aiUpdateError = this.cvStructured.aiUpdateError();

      if (this.wasUpdatingWithAi && !updatingWithAi && !aiUpdateError) {
        this.cancelSectionEdit();
        this.aiUpdateInstructions.set('');
        this.aiUpdateSectionIds.set([]);
        this.selectedSuggestionIds.set([]);
      }

      this.wasUpdatingWithAi = updatingWithAi;
    });
  }

  protected formatFileSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }

    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }

    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  protected openCvFilePicker(): void {
    this.cvFileInput()?.nativeElement.click();
  }

  protected onCvFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    input.value = '';

    if (!file) {
      return;
    }

    this.cvDocument.upload(file);
  }

  protected onExportTemplateChange(event: Event): void {
    const rawValue = readInputValue(event);
    const templateId = Number.parseInt(rawValue, 10);

    if (!Number.isInteger(templateId) || templateId < 1 || templateId > MAX_CV_EXPORT_TEMPLATE_ID) {
      return;
    }

    this.cvDocument.setExportTemplateId(templateId);
  }

  protected updateAiInstructions(event: Event): void {
    const target = event.target as HTMLTextAreaElement | null;
    this.aiUpdateInstructions.set(target?.value ?? '');
    this.cvStructured.clearAiUpdateError();
  }

  protected isAiUpdateSectionSelected(sectionId: string): boolean {
    return this.aiUpdateSectionIds().includes(sectionId);
  }

  protected toggleAiUpdateSection(sectionId: string): void {
    if (
      this.cvStructured.updatingWithAi() ||
      this.cvStructured.generatingSuggestions() ||
      this.editingSectionId() ||
      this.hasPendingSectionReorder()
    ) {
      return;
    }

    this.aiUpdateSectionIds.update((selected) =>
      selected.includes(sectionId)
        ? selected.filter((id) => id !== sectionId)
        : [...selected, sectionId]
    );
    this.cvStructured.clearAiUpdateError();
  }

  protected aiUpdateSectionLabel(section: CvStructuredSection): string {
    return section.heading?.trim() || section.sectionType || 'Untitled section';
  }

  protected isEditingSection(sectionId: string): boolean {
    return this.editingSectionId() === sectionId;
  }

  protected isSavingSection(sectionId: string): boolean {
    return this.cvStructured.savingSectionId() === sectionId;
  }

  protected sectionSaveError(sectionId: string): string | null {
    return this.isEditingSection(sectionId) ? this.cvStructured.saveError() : null;
  }

  protected beginSectionEdit(section: CvStructuredSection): void {
    if (
      this.cvStructured.savingSectionId() ||
      this.cvStructured.savingSectionOrder() ||
      this.cvStructured.updatingWithAi() ||
      this.cvStructured.generatingSuggestions() ||
      this.editingSectionId() ||
      this.hasPendingSectionReorder()
    ) {
      return;
    }

    this.cvStructured.clearSaveError();
    this.editingSectionId.set(section.id);
    this.sectionDraft.set(cloneSectionForDraft(section));
  }

  protected cancelSectionEdit(): void {
    if (this.cvStructured.savingSectionId()) {
      return;
    }

    this.editingSectionId.set(null);
    this.sectionDraft.set(null);
    this.cvStructured.clearSaveError();
  }

  protected saveSectionEdit(): void {
    const sectionId = this.editingSectionId();
    const draft = this.sectionDraft();

    if (!sectionId || !draft || !this.canSaveSection()) {
      return;
    }

    this.cvStructured.save(mergeSection(this.sections(), draft), sectionId);
  }

  protected onSectionDrop(event: CdkDragDrop<void>): void {
    if (!this.canReorderSections() || event.previousIndex === event.currentIndex) {
      return;
    }

    this.sectionOrderDraft.set(
      reorderSections(this.sections(), event.previousIndex, event.currentIndex)
    );
  }

  protected saveSectionOrder(): void {
    if (!this.canSaveSectionOrder()) {
      return;
    }

    this.cvStructured.clearSaveError();
    this.cvStructured.saveSectionOrder(this.sections());
  }

  protected discardSectionOrder(): void {
    this.sectionOrderDraft.set(null);
  }

  protected updateStructuredWithAi(): void {
    if (!this.canUpdateWithAi()) {
      return;
    }

    this.cvStructured.updateWithAi(this.aiUpdateInstructions(), this.validSectionIds(this.aiUpdateSectionIds()));
  }

  protected openAiPanel(): void {
    this.aiPanelOpen.set(true);
  }

  protected closeAiPanel(): void {
    this.aiPanelOpen.set(false);
  }

  protected toggleAiPanel(): void {
    this.aiPanelOpen.update((open) => !open);
  }

  protected generateSuggestions(): void {
    if (!this.canGenerateSuggestions()) {
      return;
    }

    this.suggestionsPanelOpen.set(true);
    this.selectedSuggestionIds.set([]);
    this.cvStructured.generateSuggestions(this.validSectionIds(this.aiUpdateSectionIds()));
  }

  protected isSuggestionSelected(suggestionId: string): boolean {
    return this.selectedSuggestionIds().includes(suggestionId);
  }

  protected toggleSuggestion(suggestionId: string): void {
    if (
      this.cvStructured.updatingWithAi() ||
      this.cvStructured.generatingSuggestions() ||
      this.editingSectionId() ||
      this.hasPendingSectionReorder()
    ) {
      return;
    }

    this.selectedSuggestionIds.update((selected) =>
      selected.includes(suggestionId)
        ? selected.filter((id) => id !== suggestionId)
        : [...selected, suggestionId]
    );
    this.cvStructured.clearAiUpdateError();
  }

  protected openSuggestionsPanel(): void {
    this.suggestionsPanelOpen.set(true);
  }

  protected closeSuggestionsPanel(): void {
    this.suggestionsPanelOpen.set(false);
  }

  protected toggleSuggestionsPanel(): void {
    this.suggestionsPanelOpen.update((open) => !open);
  }

  protected isSuggestionSectionSelected(sectionId: string): boolean {
    return this.selectedSuggestionSectionIds().has(sectionId);
  }

  private resolveSuggestionSectionId(
    suggestion: CvImprovementSuggestion,
    sections: readonly CvStructuredSection[]
  ): string | null {
    if (suggestion.sectionId) {
      return suggestion.sectionId;
    }

    if (!suggestion.entryId) {
      return null;
    }

    for (const section of sections) {
      if (section.entries.some((entry) => entry.id === suggestion.entryId)) {
        return section.id;
      }
    }

    return null;
  }

  protected applySelectedSuggestions(): void {
    const selected = this.selectedSuggestions();

    if (!this.canApplySelectedSuggestions() || selected.length === 0) {
      return;
    }

    const instructions = selected
      .map((suggestion, index) => `${index + 1}. ${this.suggestionApplyInstruction(suggestion)}`)
      .join('\n');
    const sectionIds = this.validSectionIds(
      selected
        .map((suggestion) => suggestion.sectionId)
        .filter((sectionId): sectionId is string => !!sectionId)
    );

    this.cvStructured.updateWithAi(
      `Apply these selected CV improvement suggestions:\n${instructions}`,
      sectionIds
    );
  }

  private validSectionIds(sectionIds: readonly string[]): string[] {
    const existingSectionIds = new Set(this.sections().map((section) => section.id));

    return [...new Set(sectionIds)].filter((sectionId) => existingSectionIds.has(sectionId));
  }

  private suggestionApplyInstruction(suggestion: CvImprovementSuggestion): string {
    return suggestion.suggestedInstruction?.trim() || suggestion.title.trim();
  }

  protected beginDeleteCv(): void {
    this.deleteConfirmOpen.set(true);
  }

  protected cancelDeleteCv(): void {
    this.deleteConfirmOpen.set(false);
  }

  protected confirmDeleteCv(): void {
    this.cvDocument.delete();
    this.deleteConfirmOpen.set(false);
  }
}
