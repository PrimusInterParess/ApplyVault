import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  NgZone,
  OnDestroy,
  signal,
  viewChild
} from '@angular/core';

import { CvBuilderAssistPanelComponent } from '../../components/cv-builder-assist-panel/cv-builder-assist-panel.component';
import { CvBuilderCheckExportComponent } from '../../components/cv-builder-check-export/cv-builder-check-export.component';
import { CvBuilderDocumentMenuComponent } from '../../components/cv-builder-document-menu/cv-builder-document-menu.component';
import { CvBuilderEmptyStartComponent } from '../../components/cv-builder-empty-start/cv-builder-empty-start.component';
import { CvBuilderProjectsPanelComponent } from '../../components/cv-builder-projects-panel/cv-builder-projects-panel.component';
import { CvBuilderSectionRailComponent } from '../../components/cv-builder-section-rail/cv-builder-section-rail.component';
import {
  CvBuilderDockTool,
  CvBuilderToolsDockComponent
} from '../../components/cv-builder-tools-dock/cv-builder-tools-dock.component';
import { CvUnplacedDrawerComponent } from '../../components/cv-unplaced-drawer/cv-unplaced-drawer.component';
import { CvExportTemplatePreviewComponent } from '../../components/cv-export-template-preview/cv-export-template-preview.component';
import { CvDocumentFacade } from '../../data-access/cv-document.facade';
import { CvEditSession } from '../../data-access/cv-edit-session';
import { CvProjectsFacade } from '../../data-access/cv-projects.facade';
import { CvStructuredFacade } from '../../data-access/cv-structured.facade';
import { CV_FIXED_SECTIONS, sectionIncludedOnTemplate } from '../../models/cv-fixed-sections.model';
import {
  CvImprovementSuggestion,
  CvQualityEvaluationFinding
} from '../../models/cv-structured.model';
import { sectionHasRenderableContent } from '../../utils/cv-export-template-layout.util';
import { CvTemplateInlineEdit } from '../../utils/cv-template-inline-edit.util';
import {
  appendProjectSummariesToSections,
  collectImportedProjectSummaryIds
} from '../../utils/cv-project-summary-import.util';
import { cloneSectionsForDraft } from '../../utils/cv-structured-draft.util';
import {
  CV_A4_HEIGHT_PX,
  CV_A4_WIDTH_PX,
  CV_PAGE_GAP_PX,
  cvCanvasPageSheets,
  cvCanvasStackHeight
} from '../../utils/cv-canvas-pages.util';

type CvBuilderPanel = 'assist' | 'unplaced' | 'projects' | 'checkExport';

@Component({
  selector: 'app-cv-builder-page',
  standalone: true,
  imports: [
    CommonModule,
    CvExportTemplatePreviewComponent,
    CvBuilderAssistPanelComponent,
    CvBuilderEmptyStartComponent,
    CvUnplacedDrawerComponent,
    CvBuilderProjectsPanelComponent,
    CvBuilderCheckExportComponent,
    CvBuilderSectionRailComponent,
    CvBuilderDocumentMenuComponent,
    CvBuilderToolsDockComponent
  ],
  templateUrl: './cv-builder-page.component.html',
  styleUrl: './cv-builder-page.component.scss'
})
export class CvBuilderPageComponent implements OnDestroy {
  protected readonly cvDocument = inject(CvDocumentFacade);
  protected readonly cvStructured = inject(CvStructuredFacade);
  protected readonly cvProjects = inject(CvProjectsFacade);
  protected readonly editSession = inject(CvEditSession);
  private readonly zone = inject(NgZone);

  protected readonly pdfFileInput = viewChild<ElementRef<HTMLInputElement>>('pdfFileInput');
  protected readonly profilePhotoFileInput = viewChild<ElementRef<HTMLInputElement>>('profilePhotoFileInput');
  private readonly canvasFrame = viewChild<ElementRef<HTMLElement>>('canvasFrame');
  private readonly deskScroll = viewChild<ElementRef<HTMLElement>>('deskScroll');
  private readonly paperRoot = viewChild<ElementRef<HTMLElement>>('cvPaper');

  protected readonly replaceConfirmOpen = signal(false);
  protected readonly deleteConfirmOpen = signal(false);
  protected readonly pendingPdfFile = signal<File | null>(null);
  /** Exclusive panel mutex — at most one of assist / unplaced / projects / checkExport. */
  protected readonly activePanel = signal<CvBuilderPanel | null>(null);
  /** Fit scales the A4 sheet to the center column until the user zooms. */
  protected readonly zoomMode = signal<'fit' | 'manual'>('fit');
  protected readonly zoom = signal(1);
  protected readonly paperHeight = signal(CV_A4_HEIGHT_PX);
  private readonly sheetHeight = signal(CV_A4_HEIGHT_PX);
  protected readonly activeSectionId = signal<string | null>(null);
  protected readonly a4WidthPx = CV_A4_WIDTH_PX;
  protected readonly a4HeightPx = CV_A4_HEIGHT_PX;
  protected readonly pageGapPx = CV_PAGE_GAP_PX;

  protected readonly selectedProjectSummaryIds = signal<string[]>([]);
  protected readonly importingProjectsBusy = signal(false);

  protected readonly aiUpdateInstructions = signal('');
  protected readonly aiUpdateSectionIds = signal<string[]>([]);
  protected readonly selectedSuggestionIds = signal<string[]>([]);
  protected readonly summaryProposeInstructions = signal('');

  protected readonly serverSections = this.editSession.serverSections;
  protected readonly sections = this.editSession.sections;
  protected readonly inlineDraft = this.editSession.inlineDraft;
  protected readonly saveStatus = this.editSession.saveStatus;

  protected readonly hasSections = computed(() => this.sections().length > 0);

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
   * Single busy gate for Structured mutations (Projects / Assist).
   * Covers document/structured load, save, and AI update in flight.
   */
  protected readonly editBusy = computed(
    () =>
      this.cvDocument.loading() ||
      this.cvStructured.loading() ||
      this.cvStructured.isSaving() ||
      this.cvStructured.proposingUpdate()
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

  protected readonly scaledPaperWidth = computed(() => CV_A4_WIDTH_PX * this.zoom());

  protected readonly scaledPaperHeight = computed(() => this.sheetHeight() * this.zoom());

  protected readonly pageSheets = computed(() => cvCanvasPageSheets(this.paperHeight()));

  protected readonly dockTool = computed((): CvBuilderDockTool | null => {
    const panel = this.activePanel();
    return panel === 'assist' || panel === 'unplaced' || panel === 'projects' ? panel : null;
  });

  protected readonly photoBusy = computed(
    () =>
      this.cvDocument.uploadingProfilePhoto() ||
      this.cvDocument.deletingProfilePhoto() ||
      this.cvDocument.loadingProfilePhoto()
  );

  protected readonly pageErrors = computed(() =>
    [
      this.cvDocument.profilePhotoError(),
      this.cvDocument.uploadError(),
      this.cvDocument.downloadFormattedError(),
      this.cvDocument.deleteError()
    ].filter((error): error is string => !!error)
  );

  protected readonly railItems = computed(() =>
    CV_FIXED_SECTIONS.map((slot) => {
      const section = this.sections().find((item) => item.sectionType === slot.sectionType);
      return {
        id: section?.id ?? slot.sectionType,
        heading: slot.heading,
        complete: section ? sectionHasRenderableContent(section) : false,
        canToggle: slot.sectionType !== 'Contact',
        included: section ? sectionIncludedOnTemplate(section) : true
      };
    })
  );

  private static readonly MIN_ZOOM = 0.35;
  private static readonly MAX_ZOOM = 1.35;

  private wasSavingSection = false;
  private importingProjectSectionId: string | null = null;
  /** Document id for which structured content was loaded — ignores metadata document.set echoes. */
  private structuredLoadDocumentId: string | null = null;

  constructor() {
    // Load structured only when document identity appears/changes — not on metadata-only echoes.
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

    effect((onCleanup) => {
      const frame = this.canvasFrame()?.nativeElement;
      const scroll = this.deskScroll()?.nativeElement;
      const paper = this.paperRoot()?.nativeElement;
      const sectionKey = this.sections().map((section) => section.id).join('|');

      if (!frame || !scroll || !paper || sectionKey.length === 0) {
        return;
      }

      const sync = (): void => this.syncCanvasMetrics();
      const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(sync);
      observer?.observe(frame);
      observer?.observe(paper);

      let disposed = false;
      let spy = this.watchSections(scroll);
      const raf = requestAnimationFrame(() => {
        if (disposed) {
          return;
        }

        spy?.disconnect();
        spy = this.watchSections(scroll);
      });

      onCleanup(() => {
        disposed = true;
        cancelAnimationFrame(raf);
        observer?.disconnect();
        spy?.disconnect();
      });
    });
  }

  ngOnDestroy(): void {
    this.editSession.cancelPendingSave();
    this.cvStructured.clearSuggestions();
    this.cvStructured.clearEvaluation();
    this.cvStructured.discardSummaryProposal();
    this.cvStructured.discardUpdateProposal();
  }

  /** First-visit only: create Blank CV (server seeds the seven fixed sections). */
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

  protected openDeleteConfirm(): void {
    this.deleteConfirmOpen.set(true);
  }

  protected cancelDeleteConfirm(): void {
    this.deleteConfirmOpen.set(false);
  }

  protected confirmDeleteCurrent(): void {
    this.deleteConfirmOpen.set(false);
    this.setActivePanel(null);
    this.editSession.cancelPendingSave();
    this.editSession.clearDraft();
    this.cvDocument.deleteCurrent();
  }

  protected zoomIn(): void {
    this.zoomMode.set('manual');
    this.zoom.update((value) =>
      Math.min(CvBuilderPageComponent.MAX_ZOOM, Math.round((value + 0.1) * 10) / 10)
    );
  }

  protected zoomOut(): void {
    this.zoomMode.set('manual');
    this.zoom.update((value) =>
      Math.max(CvBuilderPageComponent.MIN_ZOOM, Math.round((value - 0.1) * 10) / 10)
    );
  }

  protected setActivePanel(panel: CvBuilderPanel | null): void {
    const previous = this.activePanel();
    this.activePanel.set(panel);

    if (previous === 'assist' && panel !== 'assist') {
      this.cvStructured.discardSummaryProposal();
      this.cvStructured.discardUpdateProposal();
    }
  }

  protected selectDockTool(tool: CvBuilderDockTool): void {
    if (this.activePanel() === tool) {
      this.setActivePanel(null);
      return;
    }

    this.setActivePanel(tool);

    if (tool === 'projects') {
      this.cvProjects.loadSummaries();
    }
  }

  protected jumpToSection(sectionId: string): void {
    this.activeSectionId.set(sectionId);
    document.getElementById(`cv-section-${sectionId}`)?.scrollIntoView({
      behavior: 'smooth',
      block: 'start'
    });
  }

  protected setSectionVisibility(change: { id: string; included: boolean }): void {
    if (!this.canMutateStructured()) {
      return;
    }

    this.editSession.apply({
      kind: 'sectionVisibility',
      sectionId: change.id,
      includedOnTemplate: change.included
    });
  }

  protected openAssist(): void {
    this.setActivePanel('assist');
  }

  protected closeAssist(): void {
    if (this.activePanel() === 'assist') {
      this.setActivePanel(null);
    }
  }

  protected toggleUnplaced(): void {
    if (this.activePanel() === 'unplaced') {
      this.setActivePanel(null);
      return;
    }

    this.setActivePanel('unplaced');
  }

  protected closeUnplaced(): void {
    if (this.activePanel() === 'unplaced') {
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

  protected onInlineEdit(edit: CvTemplateInlineEdit): void {
    if (!this.canMutateStructured()) {
      return;
    }

    this.editSession.apply(edit);
  }

  protected exportPdf(): void {
    this.editSession.flushSave();
    this.cvDocument.downloadFormattedFile();
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
    this.cvStructured.proposeUpdate(instructions, sectionIds.length > 0 ? sectionIds : undefined);
  }

  protected approveUpdateProposal(): void {
    const sections = this.sections();
    this.editSession.cancelPendingSave();
    this.cvStructured.approveUpdateProposal(sections);
    this.editSession.clearDraft();
    this.selectedSuggestionIds.set([]);
  }

  protected discardUpdateProposal(): void {
    this.cvStructured.discardUpdateProposal();
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

  protected updateSummaryProposeInstructions(value: string): void {
    this.summaryProposeInstructions.set(value);
  }

  protected proposeSummary(): void {
    if (!this.hasSections()) {
      return;
    }

    this.editSession.flushSave();
    this.cvStructured.proposeSummary(this.summaryProposeInstructions());
  }

  protected approveSummaryProposal(): void {
    const sections = this.sections();
    this.editSession.cancelPendingSave();
    this.cvStructured.approveSummaryProposal(sections);
    this.editSession.clearDraft();
  }

  protected discardSummaryProposal(): void {
    this.cvStructured.discardSummaryProposal();
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

    this.cvStructured.proposeUpdate(
      `Apply these selected CV improvement suggestions:\n${instructions}`,
      sectionIds.length > 0 ? sectionIds : undefined
    );
  }

  private syncCanvasMetrics(): void {
    this.zone.run(() => {
      this.updateFitZoom();

      const paper = this.paperRoot()?.nativeElement;
      const page = paper?.querySelector('.cv-export-preview__page');

      if (page instanceof HTMLElement && page.offsetHeight > 0) {
        const contentHeight = Math.max(page.offsetHeight, page.scrollHeight);
        this.paperHeight.set(contentHeight);
        this.sheetHeight.set(cvCanvasStackHeight(contentHeight));
      }
    });
  }

  private updateFitZoom(): void {
    if (this.zoomMode() !== 'fit') {
      return;
    }

    const frame = this.canvasFrame()?.nativeElement;
    const scroll = this.deskScroll()?.nativeElement;

    if (!frame || !scroll) {
      return;
    }

    const style = getComputedStyle(scroll);
    const padX = (parseFloat(style.paddingLeft) || 0) + (parseFloat(style.paddingRight) || 0);
    const available = frame.clientWidth - padX;

    if (available < 80) {
      return;
    }

    const next = this.clampZoom(available / CV_A4_WIDTH_PX);

    if (Math.abs(next - this.zoom()) > 0.004) {
      this.zoom.set(next);
    }
  }

  private clampZoom(value: number): number {
    const clamped = Math.min(
      CvBuilderPageComponent.MAX_ZOOM,
      Math.max(CvBuilderPageComponent.MIN_ZOOM, value)
    );

    return Math.round(clamped * 100) / 100;
  }

  private watchSections(root: HTMLElement): IntersectionObserver | null {
    if (typeof IntersectionObserver === 'undefined') {
      return null;
    }

    const anchors = root.querySelectorAll<HTMLElement>('[id^="cv-section-"]');

    if (anchors.length === 0) {
      return null;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((entry) => entry.isIntersecting)
          .sort((left, right) => left.boundingClientRect.top - right.boundingClientRect.top);
        const current = visible[0]?.target.id;

        if (!current?.startsWith('cv-section-')) {
          return;
        }

        const sectionId = current.slice('cv-section-'.length);
        this.zone.run(() => this.activeSectionId.set(sectionId));
      },
      {
        root,
        rootMargin: '-8% 0px -55% 0px',
        threshold: [0, 0.15, 0.4, 0.75, 1]
      }
    );

    anchors.forEach((anchor) => observer.observe(anchor));
    return observer;
  }

  private runCreateCv(): void {
    this.cvDocument.startBlank();
  }

  private runUpload(file: File): void {
    this.cvDocument.upload(file, () => this.openUnplacedAfterImport());
  }

  protected openUnplacedAfterImport(): void {
    const count = this.cvDocument.importSummary()?.unplacedCount ?? 0;

    if (count > 0) {
      this.setActivePanel('unplaced');
    }
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
