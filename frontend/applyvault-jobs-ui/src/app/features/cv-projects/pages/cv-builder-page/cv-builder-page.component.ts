import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  OnDestroy,
  signal,
  viewChild
} from '@angular/core';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { CvBuilderAssistPanelComponent } from '../../components/cv-builder-assist-panel/cv-builder-assist-panel.component';
import { CvBuilderCheckExportComponent } from '../../components/cv-builder-check-export/cv-builder-check-export.component';
import { CvBuilderEmptyStartComponent } from '../../components/cv-builder-empty-start/cv-builder-empty-start.component';
import { CvBuilderProjectsPanelComponent } from '../../components/cv-builder-projects-panel/cv-builder-projects-panel.component';
import { CvBuilderStructurePanelComponent } from '../../components/cv-builder-structure-panel/cv-builder-structure-panel.component';
import { CvExportTemplatePreviewComponent } from '../../components/cv-export-template-preview/cv-export-template-preview.component';
import { CvDocumentFacade } from '../../data-access/cv-document.facade';
import { CvEditSession } from '../../data-access/cv-edit-session';
import { CvProjectsFacade } from '../../data-access/cv-projects.facade';
import { CvStructuredFacade } from '../../data-access/cv-structured.facade';
import {
  CV_EXPORT_TEMPLATES,
  normalizeCvExportTemplateId
} from '../../models/cv-export-template.model';
import {
  CvImprovementSuggestion,
  CvQualityEvaluationFinding,
  CvSectionType
} from '../../models/cv-structured.model';
import {
  addableSectionTypes,
  CvTemplateInlineEdit
} from '../../utils/cv-template-inline-edit.util';
import {
  appendProjectSummariesToSections,
  collectImportedProjectSummaryIds
} from '../../utils/cv-project-summary-import.util';
import { cloneSectionsForDraft, sectionHasContent } from '../../utils/cv-structured-draft.util';

type CvBuilderPanel = 'assist' | 'structure' | 'projects' | 'checkExport';

@Component({
  selector: 'app-cv-builder-page',
  standalone: true,
  imports: [
    CommonModule,
    CvExportTemplatePreviewComponent,
    CvBuilderAssistPanelComponent,
    CvBuilderEmptyStartComponent,
    CvBuilderStructurePanelComponent,
    CvBuilderProjectsPanelComponent,
    CvBuilderCheckExportComponent
  ],
  templateUrl: './cv-builder-page.component.html',
  styleUrl: './cv-builder-page.component.scss'
})
export class CvBuilderPageComponent implements OnDestroy {
  protected readonly cvDocument = inject(CvDocumentFacade);
  protected readonly cvStructured = inject(CvStructuredFacade);
  protected readonly cvProjects = inject(CvProjectsFacade);
  protected readonly editSession = inject(CvEditSession);

  protected readonly pdfFileInput = viewChild<ElementRef<HTMLInputElement>>('pdfFileInput');
  protected readonly profilePhotoFileInput = viewChild<ElementRef<HTMLInputElement>>('profilePhotoFileInput');

  protected readonly templates = CV_EXPORT_TEMPLATES;

  /** Facade is the single source of truth for Modern/Minimal (ids 2–3). */
  protected readonly selectedTemplateId = this.cvDocument.selectedExportTemplateId;
  protected readonly replaceConfirmOpen = signal(false);
  protected readonly pendingPdfFile = signal<File | null>(null);
  /** Exclusive panel mutex — at most one of assist / structure / projects / checkExport. */
  protected readonly activePanel = signal<CvBuilderPanel | null>(null);
  protected readonly pendingAddSectionType = signal<CvSectionType>('Experience');
  protected readonly zoom = signal(1);

  protected readonly selectedProjectSummaryIds = signal<string[]>([]);
  protected readonly importingProjectsBusy = signal(false);

  protected readonly aiUpdateInstructions = signal('');
  protected readonly aiUpdateSectionIds = signal<string[]>([]);
  protected readonly selectedSuggestionIds = signal<string[]>([]);

  protected readonly serverSections = this.editSession.serverSections;
  protected readonly sections = this.editSession.sections;
  protected readonly inlineDraft = this.editSession.inlineDraft;
  protected readonly saveStatus = this.editSession.saveStatus;

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

  /**
   * Single busy gate for Structured mutations (Projects / Assist / Structure).
   * Covers document/structured load, save, and AI update in flight.
   */
  protected readonly editBusy = computed(
    () =>
      this.cvDocument.loading() ||
      this.cvStructured.loading() ||
      this.cvStructured.isSaving() ||
      this.cvStructured.updatingWithAi()
  );

  protected readonly canMutateStructured = computed(
    () => this.cvDocument.hasDocument() && !this.editBusy()
  );

  protected readonly canImportSelectedProjectSummaries = computed(
    () => this.canMutateStructured() && this.selectedImportableProjectSummaries().length > 0
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

  protected readonly assistDisabled = computed(() => !this.canMutateStructured());

  /** Check export shows last saved HTML; hint when local draft/save is in flight. */
  protected readonly showingLastSavedExport = computed(
    () => this.activePanel() === 'checkExport' && (!!this.inlineDraft() || this.cvStructured.isSaving())
  );

  protected readonly zoomPercent = computed(() => Math.round(this.zoom() * 100));

  private wasUpdatingWithAi = false;
  private wasSavingSection = false;
  private importingProjectSectionId: string | null = null;
  /** Document id for which structured content was loaded — ignores prefs document.set echoes. */
  private structuredLoadDocumentId: string | null = null;

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
      const updating = this.cvStructured.updatingWithAi();

      if (this.wasUpdatingWithAi && !updating && !this.cvStructured.aiUpdateError()) {
        this.selectedSuggestionIds.set([]);
        this.setActivePanel(null);
        this.editSession.clearDraft();
      }

      this.wasUpdatingWithAi = updating;
    });

    effect(() => {
      const saving = this.cvStructured.isSaving();
      const saveError = this.cvStructured.saveError();
      const finishedSave = this.wasSavingSection && !saving;

      if (this.importingProjectSectionId && finishedSave) {
        if (!saveError) {
          this.selectedProjectSummaryIds.set([]);
        }

        this.importingProjectSectionId = null;
        this.importingProjectsBusy.set(false);
      }

      this.wasSavingSection = saving;
    });
  }

  ngOnDestroy(): void {
    this.editSession.cancelPendingSave();
    this.cvStructured.clearSuggestions();
    this.cvStructured.clearEvaluation();
  }

  protected selectTemplate(templateId: number): void {
    this.cvDocument.setExportTemplateId(normalizeCvExportTemplateId(templateId));
  }

  /** First-visit only: create Blank CV with starter sections. */
  protected startBlank(): void {
    if (this.hasStructuredCv()) {
      return;
    }

    this.runCreateCv();
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

  protected zoomIn(): void {
    this.zoom.update((value) => Math.min(1.35, Math.round((value + 0.1) * 10) / 10));
  }

  protected zoomOut(): void {
    this.zoom.update((value) => Math.max(0.7, Math.round((value - 0.1) * 10) / 10));
  }

  protected setActivePanel(panel: CvBuilderPanel | null): void {
    this.activePanel.set(panel);
  }

  protected openAssist(): void {
    this.setActivePanel('assist');
  }

  protected closeAssist(): void {
    if (this.activePanel() === 'assist') {
      this.setActivePanel(null);
    }
  }

  protected toggleStructure(): void {
    if (this.activePanel() === 'structure') {
      this.setActivePanel(null);
      return;
    }

    this.setActivePanel('structure');
    const available = this.structureSectionTypes();
    const pending = this.pendingAddSectionType();

    if (available.length > 0 && !available.includes(pending)) {
      this.pendingAddSectionType.set(available[0]);
    }
  }

  protected closeStructure(): void {
    if (this.activePanel() === 'structure') {
      this.setActivePanel(null);
    }
  }

  protected openCheckExport(): void {
    this.setActivePanel('checkExport');
    this.cvDocument.refreshExportHtmlPreview();
  }

  protected closeCheckExport(): void {
    if (this.activePanel() === 'checkExport') {
      this.setActivePanel(null);
    }
  }

  protected refreshCheckExport(): void {
    this.cvDocument.refreshExportHtmlPreview();
  }

  protected toggleProjects(): void {
    if (this.activePanel() === 'projects') {
      this.setActivePanel(null);
      return;
    }

    this.setActivePanel('projects');
    this.cvProjects.loadSummaries();
  }

  protected closeProjects(): void {
    if (this.activePanel() === 'projects') {
      this.setActivePanel(null);
    }
  }

  protected canToggleProjectSummarySelection(): boolean {
    return this.canMutateStructured();
  }

  protected toggleProjectSummarySelection(summaryId: string): void {
    if (this.importedProjectSummaryIds().has(summaryId) || !this.canToggleProjectSummarySelection()) {
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

    const nextSections = cloneSectionsForDraft(this.sections());
    const projectsSection = appendProjectSummariesToSections(nextSections, summaries);

    this.importingProjectSectionId = projectsSection.id;
    this.importingProjectsBusy.set(true);
    this.cvStructured.clearSaveError();
    this.editSession.setDraftAndPersistNow(nextSections);
  }

  protected onPendingAddSectionTypeChange(sectionType: CvSectionType): void {
    if (this.structureSectionTypes().includes(sectionType)) {
      this.pendingAddSectionType.set(sectionType);
    }
  }

  protected addSectionFromStructure(): void {
    if (!this.canMutateStructured()) {
      return;
    }

    const sectionType = this.pendingAddSectionType();

    if (!this.structureSectionTypes().includes(sectionType)) {
      return;
    }

    this.applyStructureEdit({ kind: 'addSection', sectionType });
  }

  protected moveSection(sectionId: string, direction: -1 | 1): void {
    if (!this.canMutateStructured()) {
      return;
    }

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

  protected onStructureMoveSection(event: { sectionId: string; direction: -1 | 1 }): void {
    this.moveSection(event.sectionId, event.direction);
  }

  protected removeSection(sectionId: string): void {
    if (!this.canMutateStructured()) {
      return;
    }

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

  protected onInlineEdit(edit: CvTemplateInlineEdit): void {
    if (!this.canMutateStructured()) {
      return;
    }

    if (edit.kind === 'removeSection') {
      this.removeSection(edit.sectionId);
      return;
    }

    if (edit.kind === 'addSection' || edit.kind === 'reorderSections') {
      this.applyStructureEdit(edit);
      return;
    }

    this.editSession.apply(edit);
  }

  private applyStructureEdit(
    edit: Extract<CvTemplateInlineEdit, { kind: 'addSection' | 'removeSection' | 'reorderSections' }>
  ): void {
    const next = this.editSession.apply(edit);
    const available = addableSectionTypes(next);

    if (available.length > 0 && !available.includes(this.pendingAddSectionType())) {
      this.pendingAddSectionType.set(available[0]);
    }
  }

  protected exportPdf(): void {
    this.editSession.flushSave();
    this.cvDocument.downloadFormattedFile();
  }

  protected onTemplateSelectChange(event: Event): void {
    const templateId = Number.parseInt(readInputValue(event), 10);

    if (Number.isInteger(templateId) && (templateId === 2 || templateId === 3)) {
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

    this.editSession.flushSave();
    const sectionIds = this.validSectionIds(this.aiUpdateSectionIds());
    this.cvStructured.updateWithAi(instructions, sectionIds.length > 0 ? sectionIds : undefined);
  }

  protected generateSuggestions(): void {
    if (!this.hasSections()) {
      return;
    }

    this.editSession.flushSave();
    this.selectedSuggestionIds.set([]);
    const sectionIds = this.validSectionIds(this.aiUpdateSectionIds());
    this.cvStructured.generateSuggestions(sectionIds.length > 0 ? sectionIds : undefined);
  }

  protected evaluateQuality(): void {
    if (!this.hasSections()) {
      return;
    }

    this.editSession.flushSave();
    this.cvStructured.evaluateQuality();
  }

  /**
   * D5: copy evaluation finding into Update-with-instructions; pre-select section chip.
   * Does not invoke Update CV with AI.
   */
  protected useFindingInAssist(finding: CvQualityEvaluationFinding): void {
    if (this.assistDisabled()) {
      return;
    }

    this.aiUpdateInstructions.set(this.findingAssistInstruction(finding));

    const sectionId = finding.sectionId?.trim();
    if (sectionId) {
      const matched = this.validSectionIds([sectionId]);
      if (matched.length > 0) {
        this.aiUpdateSectionIds.set(matched);
      }
    }

    this.cvStructured.clearAiUpdateError();
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

  private runCreateCv(): void {
    this.cvDocument.setExportTemplateId(this.selectedTemplateId());
    this.cvDocument.startBlankWithStarterSections();
  }

  private runUpload(file: File): void {
    this.cvDocument.setExportTemplateId(this.selectedTemplateId());
    this.cvDocument.upload(file);
  }

  private validSectionIds(sectionIds: readonly string[]): string[] {
    const existing = new Set(this.sections().map((section) => section.id));
    return [...new Set(sectionIds)].filter((id) => existing.has(id));
  }

  private suggestionApplyInstruction(suggestion: CvImprovementSuggestion): string {
    return suggestion.suggestedInstruction?.trim() || suggestion.title.trim();
  }

  private findingAssistInstruction(finding: CvQualityEvaluationFinding): string {
    const title = finding.title.trim();
    const detail = finding.detail.trim();
    const severity = finding.severity.trim();
    const dimension = finding.dimension.trim();
    const metaParts = [
      severity ? `Severity: ${severity}` : '',
      dimension ? `Dimension: ${dimension}` : ''
    ].filter(Boolean);
    const meta = metaParts.length > 0 ? `${metaParts.join(' · ')}\n` : '';
    const body = detail && detail !== title ? `${title}\n\n${detail}` : title || detail;
    return `${meta}${body}`.trim();
  }
}
