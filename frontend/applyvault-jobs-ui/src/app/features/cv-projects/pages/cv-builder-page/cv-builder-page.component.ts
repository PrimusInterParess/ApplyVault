import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, OnDestroy, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { CvBuilderAssistPanelComponent } from '../../components/cv-builder-assist-panel/cv-builder-assist-panel.component';
import { CvExportTemplatePreviewComponent } from '../../components/cv-export-template-preview/cv-export-template-preview.component';
import { CvDocumentFacade } from '../../data-access/cv-document.facade';
import { CvStructuredFacade } from '../../data-access/cv-structured.facade';
import {
  CV_EXPORT_MAX_PAGE_OPTIONS,
  CV_EXPORT_TEMPLATES,
  DEFAULT_CV_EXPORT_TEMPLATE_ID,
  MAX_CV_EXPORT_TEMPLATE_ID
} from '../../models/cv-export-template.model';
import { CvImprovementSuggestion, CvStructuredSection } from '../../models/cv-structured.model';
import {
  applyCvTemplateInlineEdit,
  CvTemplateInlineEdit
} from '../../utils/cv-template-inline-edit.util';
import { sectionsAreEqual } from '../../utils/cv-structured-draft.util';

type BuilderStep = 'pick' | 'edit';

@Component({
  selector: 'app-cv-builder-page',
  standalone: true,
  imports: [CommonModule, CvExportTemplatePreviewComponent, CvBuilderAssistPanelComponent, RouterLink],
  templateUrl: './cv-builder-page.component.html',
  styleUrl: './cv-builder-page.component.scss'
})
export class CvBuilderPageComponent implements OnDestroy {
  protected readonly cvDocument = inject(CvDocumentFacade);
  protected readonly cvStructured = inject(CvStructuredFacade);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly templates = CV_EXPORT_TEMPLATES;
  protected readonly maxPageOptions = CV_EXPORT_MAX_PAGE_OPTIONS;

  protected readonly step = signal<BuilderStep>('pick');
  protected readonly selectedTemplateId = signal(DEFAULT_CV_EXPORT_TEMPLATE_ID);
  protected readonly replaceConfirmOpen = signal(false);
  protected readonly assistOpen = signal(false);
  protected readonly zoom = signal(1);

  protected readonly inlineDraft = signal<CvStructuredSection[] | null>(null);

  protected readonly aiUpdateInstructions = signal('');
  protected readonly aiUpdateSectionIds = signal<string[]>([]);
  protected readonly selectedSuggestionIds = signal<string[]>([]);

  protected readonly serverSections = computed(() => {
    const items = this.cvStructured.structured()?.sections ?? [];
    return [...items].sort((left, right) => left.sortOrder - right.sortOrder);
  });

  protected readonly sections = computed(() => this.inlineDraft() ?? this.serverSections());

  protected readonly hasSections = computed(() => this.sections().length > 0);

  protected readonly assistDisabled = computed(
    () => this.cvStructured.savingSectionId() !== null || this.cvStructured.savingSectionOrder()
  );

  protected readonly saveStatus = computed(() => {
    if (this.cvStructured.savingSectionId()) {
      return 'Saving…';
    }

    if (this.inlineDraft()) {
      return 'Unsaved changes';
    }

    return 'Saved';
  });

  protected readonly zoomPercent = computed(() => Math.round(this.zoom() * 100));

  protected readonly selectedTemplateLabel = computed(
    () => this.templates.find((template) => template.id === this.selectedTemplateId())?.label ?? 'Template'
  );

  private wasUpdatingWithAi = false;
  private wasSavingSection = false;
  private saveTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    const templateFromStorage = this.cvDocument.selectedExportTemplateId();
    this.selectedTemplateId.set(templateFromStorage);

    effect(() => {
      if (!this.cvDocument.loading() && this.cvDocument.hasDocument()) {
        this.cvStructured.load();
      }
    });

    effect(() => {
      const sections = this.serverSections();

      if (sections.length > 0 && this.route.snapshot.queryParamMap.get('step') === 'edit') {
        this.step.set('edit');
      }
    });

    effect(() => {
      const updating = this.cvStructured.updatingWithAi();

      if (this.wasUpdatingWithAi && !updating && !this.cvStructured.aiUpdateError()) {
        this.selectedSuggestionIds.set([]);
        this.assistOpen.set(false);
        this.inlineDraft.set(null);
      }

      this.wasUpdatingWithAi = updating;
    });

    effect(() => {
      const savingId = this.cvStructured.savingSectionId();

      if (this.wasSavingSection && !savingId && !this.cvStructured.saveError()) {
        const draft = this.inlineDraft();

        if (!draft || sectionsAreEqual(draft, this.serverSections())) {
          this.inlineDraft.set(null);
        }
      }

      this.wasSavingSection = savingId !== null;
    });
  }

  ngOnDestroy(): void {
    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
    }

    this.cvStructured.clearSuggestions();
  }

  protected selectTemplate(templateId: number): void {
    this.selectedTemplateId.set(templateId);
    this.cvDocument.setExportTemplateId(templateId);
  }

  protected onMaxPagesChange(event: Event): void {
    const raw = readInputValue(event);
    const parsed = raw === '' ? null : Number.parseInt(raw, 10);
    this.cvDocument.setExportMaxPages(Number.isInteger(parsed) ? parsed : null);
  }

  protected beginCreateCv(): void {
    if (this.cvDocument.hasDocument()) {
      this.replaceConfirmOpen.set(true);
      return;
    }

    this.runCreateCv();
  }

  protected cancelReplaceConfirm(): void {
    this.replaceConfirmOpen.set(false);
  }

  protected confirmReplaceAndCreate(): void {
    this.replaceConfirmOpen.set(false);
    this.runCreateCv();
  }

  protected continueEditing(): void {
    this.step.set('edit');
    void this.router.navigate([], { relativeTo: this.route, queryParams: { step: 'edit' } });
  }

  protected backToTemplates(): void {
    this.step.set('pick');
    void this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  protected zoomIn(): void {
    this.zoom.update((value) => Math.min(1.35, Math.round((value + 0.1) * 10) / 10));
  }

  protected zoomOut(): void {
    this.zoom.update((value) => Math.max(0.7, Math.round((value - 0.1) * 10) / 10));
  }

  protected openAssist(): void {
    this.assistOpen.set(true);
  }

  protected closeAssist(): void {
    this.assistOpen.set(false);
  }

  protected onInlineEdit(edit: CvTemplateInlineEdit): void {
    const next = applyCvTemplateInlineEdit(this.sections(), edit);
    this.inlineDraft.set(next);
    this.scheduleSave(next);
  }

  protected exportPdf(): void {
    this.flushSave();
    this.cvDocument.downloadFormatted();
  }

  protected onTemplateSelectChange(event: Event): void {
    const templateId = Number.parseInt(readInputValue(event), 10);

    if (Number.isInteger(templateId) && templateId >= 1 && templateId <= MAX_CV_EXPORT_TEMPLATE_ID) {
      this.selectTemplate(templateId);
    }
  }

  protected toggleAiUpdateSection(sectionId: string): void {
    if (this.assistDisabled()) {
      return;
    }

    this.aiUpdateSectionIds.update((selected) =>
      selected.includes(sectionId) ? selected.filter((id) => id !== sectionId) : [...selected, sectionId]
    );
    this.cvStructured.clearAiUpdateError();
  }

  protected updateAiInstructions(value: string): void {
    this.aiUpdateInstructions.set(value);
  }

  protected updateStructuredWithAi(): void {
    const instructions = this.aiUpdateInstructions().trim();

    if (!instructions) {
      return;
    }

    this.flushSave();
    const sectionIds = this.validSectionIds(this.aiUpdateSectionIds());
    this.cvStructured.updateWithAi(instructions, sectionIds.length > 0 ? sectionIds : undefined);
  }

  protected generateSuggestions(): void {
    if (!this.hasSections()) {
      return;
    }

    this.flushSave();
    this.selectedSuggestionIds.set([]);
    const sectionIds = this.validSectionIds(this.aiUpdateSectionIds());
    this.cvStructured.generateSuggestions(sectionIds.length > 0 ? sectionIds : undefined);
  }

  protected toggleSuggestion(suggestionId: string): void {
    if (this.assistDisabled()) {
      return;
    }

    this.selectedSuggestionIds.update((selected) =>
      selected.includes(suggestionId) ? selected.filter((id) => id !== suggestionId) : [...selected, suggestionId]
    );
  }

  protected applySelectedSuggestions(): void {
    const selected = this.cvStructured
      .suggestions()
      .filter((suggestion) => this.selectedSuggestionIds().includes(suggestion.id));

    if (selected.length === 0) {
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
      sectionIds.length > 0 ? sectionIds : undefined
    );
  }

  private scheduleSave(sections: CvStructuredSection[]): void {
    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
    }

    this.saveTimer = setTimeout(() => {
      this.persistSections(sections);
    }, 500);
  }

  private flushSave(): void {
    const draft = this.inlineDraft();

    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
      this.saveTimer = null;
    }

    if (draft) {
      this.persistSections(draft);
    }
  }

  private persistSections(sections: CvStructuredSection[]): void {
    const anchorSectionId = sections[0]?.id;

    if (!anchorSectionId) {
      return;
    }

    this.cvStructured.save(sections, anchorSectionId);
  }

  private runCreateCv(): void {
    this.cvDocument.setExportTemplateId(this.selectedTemplateId());
    this.cvDocument.startBlankWithStarterSections(() => {
      this.step.set('edit');
      void this.router.navigate([], { relativeTo: this.route, queryParams: { step: 'edit' } });
    });
  }

  private validSectionIds(sectionIds: readonly string[]): string[] {
    const existing = new Set(this.sections().map((section) => section.id));
    return [...new Set(sectionIds)].filter((id) => existing.has(id));
  }

  private suggestionApplyInstruction(suggestion: CvImprovementSuggestion): string {
    return suggestion.suggestedInstruction?.trim() || suggestion.title.trim();
  }
}
