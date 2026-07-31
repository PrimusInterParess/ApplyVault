import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  effect,
  ElementRef,
  HostListener,
  inject,
  OnDestroy,
  signal,
  viewChild
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs/operators';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { CvBuilderAssistPanelComponent } from '../../components/cv-builder-assist-panel/cv-builder-assist-panel.component';
import { CvExportHtmlPreviewComponent } from '../../components/cv-export-html-preview/cv-export-html-preview.component';
import { CvExportTemplatePreviewComponent } from '../../components/cv-export-template-preview/cv-export-template-preview.component';
import { CvDocumentFacade } from '../../data-access/cv-document.facade';
import { CvProjectsFacade } from '../../data-access/cv-projects.facade';
import { CvStructuredFacade } from '../../data-access/cv-structured.facade';
import {
  CV_EXPORT_MAX_PAGE_OPTIONS,
  CV_EXPORT_TEMPLATES,
  MAX_CV_EXPORT_TEMPLATE_ID,
  normalizeCvExportTemplateId
} from '../../models/cv-export-template.model';
import {
  CvImprovementSuggestion,
  CvSectionType,
  CvStructuredSection
} from '../../models/cv-structured.model';
import {
  addableSectionTypes,
  applyCvTemplateInlineEdit,
  CvTemplateInlineEdit
} from '../../utils/cv-template-inline-edit.util';
import {
  appendProjectSummariesToSections,
  collectImportedProjectSummaryIds
} from '../../utils/cv-project-summary-import.util';
import {
  cloneSectionsForDraft,
  sectionHasContent,
  sectionsAreEqual
} from '../../utils/cv-structured-draft.util';
import { normalizeSectionsForEditing } from '../../utils/cv-structured-edit-normalizer.util';

type BuilderStep = 'pick' | 'edit';

@Component({
  selector: 'app-cv-builder-page',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    CvExportHtmlPreviewComponent,
    CvExportTemplatePreviewComponent,
    CvBuilderAssistPanelComponent
  ],
  templateUrl: './cv-builder-page.component.html',
  styleUrl: './cv-builder-page.component.scss'
})
export class CvBuilderPageComponent implements OnDestroy {
  protected readonly cvDocument = inject(CvDocumentFacade);
  protected readonly cvStructured = inject(CvStructuredFacade);
  protected readonly cvProjects = inject(CvProjectsFacade);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly pdfFileInput = viewChild<ElementRef<HTMLInputElement>>('pdfFileInput');
  protected readonly profilePhotoFileInput = viewChild<ElementRef<HTMLInputElement>>('profilePhotoFileInput');

  protected readonly templates = CV_EXPORT_TEMPLATES;
  protected readonly maxPageOptions = CV_EXPORT_MAX_PAGE_OPTIONS;

  protected readonly step = signal<BuilderStep>('pick');
  /** Facade is the single source of truth for Classic/Modern/Minimal (ids 1–3). */
  protected readonly selectedTemplateId = this.cvDocument.selectedExportTemplateId;
  protected readonly replaceConfirmOpen = signal(false);
  protected readonly pendingPdfFile = signal<File | null>(null);
  protected readonly assistOpen = signal(false);
  protected readonly structureOpen = signal(false);
  protected readonly projectsOpen = signal(false);
  protected readonly checkExportOpen = signal(false);
  protected readonly pendingAddSectionType = signal<CvSectionType>('Experience');
  protected readonly zoom = signal(1);
  private checkExportTriggerEl: HTMLElement | null = null;

  protected readonly inlineDraft = signal<CvStructuredSection[] | null>(null);
  protected readonly selectedProjectSummaryIds = signal<string[]>([]);
  protected readonly importingProjectsBusy = signal(false);

  protected readonly aiUpdateInstructions = signal('');
  protected readonly aiUpdateSectionIds = signal<string[]>([]);
  protected readonly selectedSuggestionIds = signal<string[]>([]);

  private readonly stepQuery = toSignal(
    this.route.queryParamMap.pipe(map((params) => params.get('step'))),
    { initialValue: this.route.snapshot.queryParamMap.get('step') }
  );

  protected readonly serverSections = computed(() => {
    const items = this.cvStructured.structured()?.sections ?? [];
    return [...items].sort((left, right) => left.sortOrder - right.sortOrder);
  });

  protected readonly sections = computed(() => this.inlineDraft() ?? this.serverSections());

  protected readonly hasSections = computed(() => this.sections().length > 0);

  protected readonly structureSectionTypes = computed(() => addableSectionTypes(this.sections()));

  protected readonly importedProjectSummaryIds = computed(() =>
    collectImportedProjectSummaryIds(this.sections())
  );

  protected readonly selectedImportableProjectSummaries = computed(() => {
    const selectedIds = new Set(this.selectedProjectSummaryIds());
    const importedIds = this.importedProjectSummaryIds();

    return this.cvProjects
      .savedSummaries()
      .filter((summary) => selectedIds.has(summary.id) && !importedIds.has(summary.id));
  });

  protected readonly canImportSelectedProjectSummaries = computed(
    () =>
      this.selectedImportableProjectSummaries().length > 0 &&
      this.cvDocument.hasDocument() &&
      !this.cvDocument.loading() &&
      !this.cvStructured.loading() &&
      !this.cvStructured.savingSectionId() &&
      !this.cvStructured.savingSectionOrder() &&
      !this.cvStructured.updatingWithAi()
  );

  /** True when a Structured CV already exists (document + structured content/sections). */
  protected readonly hasStructuredCv = computed(() => {
    const document = this.cvDocument.document();

    if (!document) {
      return false;
    }

    if (document.hasStructuredContent) {
      return true;
    }

    return this.serverSections().length > 0;
  });

  protected readonly assistDisabled = computed(() => this.cvStructured.isSaving());

  protected readonly saveStatus = computed(() => {
    if (this.cvStructured.isSaving()) {
      return 'Saving…';
    }

    if (this.inlineDraft()) {
      return 'Unsaved changes';
    }

    return 'Saved';
  });

  /** Check export shows last saved HTML; hint when local draft/save is in flight. */
  protected readonly showingLastSavedExport = computed(
    () => this.checkExportOpen() && (!!this.inlineDraft() || this.cvStructured.isSaving())
  );

  protected readonly zoomPercent = computed(() => Math.round(this.zoom() * 100));

  protected readonly selectedTemplateLabel = computed(
    () => this.templates.find((template) => template.id === this.selectedTemplateId())?.label ?? 'Template'
  );

  private wasUpdatingWithAi = false;
  private wasSavingSection = false;
  private importingProjectSectionId: string | null = null;
  private saveTimer: ReturnType<typeof setTimeout> | null = null;
  private pickFidelityTimer: ReturnType<typeof setTimeout> | null = null;
  private editCanvasNormalizedForDocumentId: string | null = null;
  /** Document id for which structured content was loaded — ignores prefs document.set echoes. */
  private structuredLoadDocumentId: string | null = null;
  /** Local edit generation; cleared only when matching save generation succeeds. */
  private editGeneration = 0;
  private editGenerationAtLastSaveRequest = 0;
  private lastSaveRequestGeneration = 0;
  private seenSuccessfulSaveGeneration = 0;

  constructor() {
    // Load structured only when document identity appears/changes — not on export-prefs metadata echo.
    effect(() => {
      if (this.cvDocument.loading()) {
        return;
      }

      const document = this.cvDocument.document();

      if (!document) {
        this.structuredLoadDocumentId = null;
        return;
      }

      if (this.structuredLoadDocumentId === document.id) {
        return;
      }

      if (this.cvStructured.isSaving()) {
        return;
      }

      this.structuredLoadDocumentId = document.id;
      this.cvStructured.load();
    });

    effect(() => {
      const stepParam = this.stepQuery();

      if (stepParam === 'pick') {
        this.step.set('pick');
        return;
      }

      if (this.hasStructuredCv() && !this.cvDocument.loading() && !this.cvStructured.loading()) {
        this.step.set('edit');
      }
    });

    // Pick gallery/stage: export HTML fidelity when a Structured CV exists.
    effect(() => {
      if (this.step() !== 'pick') {
        return;
      }

      if (!this.hasStructuredCv() || this.cvDocument.loading() || this.cvStructured.loading()) {
        return;
      }

      this.cvDocument.selectedExportMaxPages();
      this.serverSections();
      this.cvDocument.document();
      this.schedulePickFidelityRefresh();
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
      const saving = this.cvStructured.isSaving();
      const saveError = this.cvStructured.saveError();
      const successfulGeneration = this.cvStructured.lastSuccessfulSaveGeneration();
      const finishedSave = this.wasSavingSection && !saving;

      if (this.importingProjectSectionId && finishedSave) {
        if (!saveError) {
          this.selectedProjectSummaryIds.set([]);
        }

        this.importingProjectSectionId = null;
        this.importingProjectsBusy.set(false);
      }

      // Clear draft on successful latest save generation when no newer local edit exists.
      if (
        successfulGeneration > 0 &&
        successfulGeneration !== this.seenSuccessfulSaveGeneration &&
        successfulGeneration === this.lastSaveRequestGeneration &&
        this.editGeneration === this.editGenerationAtLastSaveRequest
      ) {
        this.seenSuccessfulSaveGeneration = successfulGeneration;
        this.inlineDraft.set(null);
      } else if (finishedSave && !saveError) {
        // Secondary: equality clear for edge cases (AI/import paths) after normalize harden.
        const draft = this.inlineDraft();

        if (!draft || sectionsAreEqual(draft, this.serverSections())) {
          this.inlineDraft.set(null);
        }
      }

      this.wasSavingSection = saving;
    });

    // ADR-0003: seed edit-canvas canonical shapes on edit enter / canvas mount.
    effect(() => {
      if (this.step() !== 'edit') {
        this.editCanvasNormalizedForDocumentId = null;
        return;
      }

      if (this.cvDocument.loading() || this.cvStructured.loading()) {
        return;
      }

      const documentId = this.cvDocument.document()?.id ?? null;
      this.serverSections();

      if (!documentId || this.sections().length === 0) {
        return;
      }

      if (this.editCanvasNormalizedForDocumentId === documentId) {
        return;
      }

      this.editCanvasNormalizedForDocumentId = documentId;
      this.ensureContentEditShape();
    });
  }

  ngOnDestroy(): void {
    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
    }

    if (this.pickFidelityTimer) {
      clearTimeout(this.pickFidelityTimer);
    }

    this.cvStructured.clearSuggestions();
  }

  @HostListener('document:keydown.escape')
  protected onDocumentEscape(): void {
    if (this.checkExportOpen()) {
      this.closeCheckExport();
    }
  }

  protected pickFidelitySrcdoc(templateId: number) {
    return this.cvDocument.pickFidelitySrcdoc(templateId);
  }

  protected pickFidelityNotice(templateId: number): string | null {
    return this.cvDocument.pickFidelityNotice(templateId);
  }

  protected selectTemplate(templateId: number): void {
    this.cvDocument.setExportTemplateId(normalizeCvExportTemplateId(templateId));
  }

  protected onMaxPagesChange(event: Event): void {
    const raw = readInputValue(event);
    const parsed = raw === '' ? null : Number.parseInt(raw, 10);
    this.cvDocument.setExportMaxPages(Number.isInteger(parsed) ? parsed : null);
  }

  /** First-visit only: create Blank CV with starter sections, then enter edit. */
  protected startBlank(): void {
    if (this.hasStructuredCv()) {
      return;
    }

    this.runCreateCv();
  }

  /** Existing Structured CV: keep content, apply selected Template layout, enter edit. */
  protected continueWithTemplate(): void {
    this.cvDocument.setExportTemplateId(this.selectedTemplateId());
    this.enterEdit();
  }

  protected openPdfPicker(): void {
    this.pdfFileInput()?.nativeElement.click();
  }

  protected openProfilePhotoFilePicker(): void {
    this.profilePhotoFileInput()?.nativeElement.click();
  }

  protected onProfilePhotoFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    input.value = '';

    if (!file) {
      return;
    }

    this.cvDocument.uploadProfilePhoto(file);
  }

  protected onPdfSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    input.value = '';

    if (!file) {
      return;
    }

    if (this.hasStructuredCv()) {
      this.pendingPdfFile.set(file);
      this.replaceConfirmOpen.set(true);
      return;
    }

    this.runUpload(file);
  }

  protected cancelReplaceConfirm(): void {
    this.replaceConfirmOpen.set(false);
    this.pendingPdfFile.set(null);
  }

  protected confirmReplaceAndUpload(): void {
    const file = this.pendingPdfFile();
    this.replaceConfirmOpen.set(false);
    this.pendingPdfFile.set(null);

    if (file) {
      this.runUpload(file);
    }
  }

  protected continueEditing(): void {
    this.enterEdit();
  }

  protected backToTemplates(): void {
    this.step.set('pick');
    void this.router.navigate([], { relativeTo: this.route, queryParams: { step: 'pick' } });
  }

  protected zoomIn(): void {
    this.zoom.update((value) => Math.min(1.35, Math.round((value + 0.1) * 10) / 10));
  }

  protected zoomOut(): void {
    this.zoom.update((value) => Math.max(0.7, Math.round((value - 0.1) * 10) / 10));
  }

  protected openAssist(): void {
    this.assistOpen.set(true);
    this.structureOpen.set(false);
    this.projectsOpen.set(false);
    this.checkExportOpen.set(false);
  }

  protected closeAssist(): void {
    this.assistOpen.set(false);
  }

  protected toggleStructure(): void {
    this.structureOpen.update((open) => !open);

    if (this.structureOpen()) {
      this.assistOpen.set(false);
      this.projectsOpen.set(false);
      this.checkExportOpen.set(false);
      const available = this.structureSectionTypes();
      const pending = this.pendingAddSectionType();

      if (available.length > 0 && !available.includes(pending)) {
        this.pendingAddSectionType.set(available[0]);
      }
    }
  }

  protected closeStructure(): void {
    this.structureOpen.set(false);
  }

  /**
   * Seed the edit canvas with canonical edit shapes (Summary/Skills slots + Contact
   * multi-bullet expand) so fields show and channel edits target stable entry ids.
   * Persists when normalization changes the payload.
   */
  private ensureContentEditShape(): void {
    const current = this.sections();

    if (current.length === 0) {
      return;
    }

    const normalized = normalizeSectionsForEditing(current);

    if (sectionsAreEqual(normalized, current)) {
      return;
    }

    this.editGeneration++;
    this.inlineDraft.set(normalized);
    this.scheduleSave(normalized);
  }

  protected openCheckExport(): void {
    const active = document.activeElement;
    this.checkExportTriggerEl = active instanceof HTMLElement ? active : null;
    this.assistOpen.set(false);
    this.structureOpen.set(false);
    this.projectsOpen.set(false);
    this.checkExportOpen.set(true);
    this.cvDocument.refreshExportHtmlPreview();

    queueMicrotask(() => {
      const dialog = document.getElementById('cv-builder-check-export-dialog');
      dialog?.focus();
    });
  }

  protected closeCheckExport(): void {
    this.checkExportOpen.set(false);
    const trigger = this.checkExportTriggerEl;
    this.checkExportTriggerEl = null;

    queueMicrotask(() => {
      trigger?.focus();
    });
  }

  protected refreshCheckExport(): void {
    this.cvDocument.refreshExportHtmlPreview();
  }

  protected toggleProjects(): void {
    this.projectsOpen.update((open) => !open);

    if (this.projectsOpen()) {
      this.assistOpen.set(false);
      this.structureOpen.set(false);
      this.checkExportOpen.set(false);
      this.cvProjects.loadSummaries();
    }
  }

  protected closeProjects(): void {
    this.projectsOpen.set(false);
  }

  protected isProjectSummaryImported(summaryId: string): boolean {
    return this.importedProjectSummaryIds().has(summaryId);
  }

  protected isProjectSummarySelected(summaryId: string): boolean {
    return this.selectedProjectSummaryIds().includes(summaryId);
  }

  protected canToggleProjectSummarySelection(): boolean {
    return (
      this.cvDocument.hasDocument() &&
      !this.cvDocument.loading() &&
      !this.cvStructured.loading() &&
      !this.cvStructured.savingSectionId() &&
      !this.cvStructured.savingSectionOrder() &&
      !this.cvStructured.updatingWithAi()
    );
  }

  protected toggleProjectSummarySelection(summaryId: string): void {
    if (this.isProjectSummaryImported(summaryId) || !this.canToggleProjectSummarySelection()) {
      return;
    }

    this.selectedProjectSummaryIds.update((selected) =>
      selected.includes(summaryId)
        ? selected.filter((id) => id !== summaryId)
        : [...selected, summaryId]
    );
    this.cvStructured.clearSaveError();
  }

  protected importSelectedProjectSummaries(): void {
    const summaries = this.selectedImportableProjectSummaries();

    if (!this.canImportSelectedProjectSummaries() || summaries.length === 0) {
      return;
    }

    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
      this.saveTimer = null;
    }

    const nextSections = cloneSectionsForDraft(this.sections());
    const projectsSection = appendProjectSummariesToSections(nextSections, summaries);

    this.editGeneration++;
    this.inlineDraft.set(nextSections);
    this.importingProjectSectionId = projectsSection.id;
    this.importingProjectsBusy.set(true);
    this.cvStructured.clearSaveError();
    this.editGenerationAtLastSaveRequest = this.editGeneration;
    this.lastSaveRequestGeneration = this.cvStructured.save(nextSections, projectsSection.id);
  }

  protected onPendingAddSectionTypeChange(event: Event): void {
    const value = readInputValue(event) as CvSectionType;

    if (this.structureSectionTypes().includes(value)) {
      this.pendingAddSectionType.set(value);
    }
  }

  protected addSectionFromStructure(): void {
    const sectionType = this.pendingAddSectionType();

    if (!this.structureSectionTypes().includes(sectionType)) {
      return;
    }

    this.applyStructureEdit({ kind: 'addSection', sectionType });
  }

  protected moveSection(sectionId: string, direction: -1 | 1): void {
    const items = this.sections();
    const fromIndex = items.findIndex((section) => section.id === sectionId);

    if (fromIndex < 0) {
      return;
    }

    const toIndex = fromIndex + direction;

    if (toIndex < 0 || toIndex >= items.length) {
      return;
    }

    this.applyStructureEdit({ kind: 'reorderSections', fromIndex, toIndex });
  }

  protected removeSection(sectionId: string): void {
    const section = this.sections().find((item) => item.id === sectionId);

    if (!section) {
      return;
    }

    if (
      sectionHasContent(section) &&
      !window.confirm(`Remove “${section.heading.trim() || section.sectionType}” and its content?`)
    ) {
      return;
    }

    this.applyStructureEdit({ kind: 'removeSection', sectionId });
  }

  protected canMoveSectionUp(sectionId: string): boolean {
    return this.sections().findIndex((section) => section.id === sectionId) > 0;
  }

  protected canMoveSectionDown(sectionId: string): boolean {
    const items = this.sections();
    const index = items.findIndex((section) => section.id === sectionId);
    return index >= 0 && index < items.length - 1;
  }

  protected onInlineEdit(edit: CvTemplateInlineEdit): void {
    if (edit.kind === 'removeSection') {
      this.removeSection(edit.sectionId);
      return;
    }

    if (edit.kind === 'addSection' || edit.kind === 'reorderSections') {
      this.applyStructureEdit(edit);
      return;
    }

    const next = applyCvTemplateInlineEdit(this.sections(), edit);
    this.editGeneration++;
    this.inlineDraft.set(next);
    this.scheduleSave(next);
  }

  private applyStructureEdit(
    edit: Extract<CvTemplateInlineEdit, { kind: 'addSection' | 'removeSection' | 'reorderSections' }>
  ): void {
    const next = applyCvTemplateInlineEdit(this.sections(), edit);
    this.editGeneration++;
    this.inlineDraft.set(next);
    this.scheduleSave(next);

    const available = addableSectionTypes(next);

    if (available.length > 0 && !available.includes(this.pendingAddSectionType())) {
      this.pendingAddSectionType.set(available[0]);
    }
  }

  protected exportPdf(): void {
    this.flushSave();
    this.cvDocument.downloadFormattedFile();
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

  private enterEdit(): void {
    this.editCanvasNormalizedForDocumentId = null;
    this.step.set('edit');
    this.ensureContentEditShape();
    void this.router.navigate([], { relativeTo: this.route, queryParams: { step: 'edit' } });
  }

  private scheduleSave(sections: CvStructuredSection[]): void {
    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
    }

    this.saveTimer = setTimeout(() => {
      this.persistSections(sections);
    }, 500);
  }

  private schedulePickFidelityRefresh(): void {
    if (this.pickFidelityTimer) {
      clearTimeout(this.pickFidelityTimer);
    }

    this.pickFidelityTimer = setTimeout(() => {
      if (this.cvStructured.isSaving()) {
        this.schedulePickFidelityRefresh();
        return;
      }

      this.cvDocument.refreshPickFidelityPreviews();
    }, 200);
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

    this.editGenerationAtLastSaveRequest = this.editGeneration;
    this.lastSaveRequestGeneration = this.cvStructured.save(sections, anchorSectionId);
  }

  private runCreateCv(): void {
    this.cvDocument.setExportTemplateId(this.selectedTemplateId());
    this.cvDocument.startBlankWithStarterSections(() => {
      this.enterEdit();
    });
  }

  private runUpload(file: File): void {
    this.cvDocument.setExportTemplateId(this.selectedTemplateId());
    this.cvDocument.upload(file, () => {
      this.enterEdit();
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
