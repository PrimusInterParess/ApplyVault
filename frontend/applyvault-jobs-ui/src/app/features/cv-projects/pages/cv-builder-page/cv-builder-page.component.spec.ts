import { Component, input, output, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';

import { CvBuilderCheckExportComponent } from '../../components/cv-builder-check-export/cv-builder-check-export.component';
import { CvBuilderEmptyStartComponent } from '../../components/cv-builder-empty-start/cv-builder-empty-start.component';
import { CvBuilderProjectsPanelComponent } from '../../components/cv-builder-projects-panel/cv-builder-projects-panel.component';
import { CvBuilderStructurePanelComponent } from '../../components/cv-builder-structure-panel/cv-builder-structure-panel.component';
import { CvDocumentFacade } from '../../data-access/cv-document.facade';
import { CvEditSession } from '../../data-access/cv-edit-session';
import { CvProjectsFacade } from '../../data-access/cv-projects.facade';
import { CvStructuredFacade } from '../../data-access/cv-structured.facade';
import { CvStructuredSection } from '../../models/cv-structured.model';
import { CvBuilderPageComponent } from './cv-builder-page.component';

@Component({
  selector: 'app-cv-export-template-preview',
  standalone: true,
  template: '<div class="stub-template-preview"></div>'
})
class StubCvExportTemplatePreviewComponent {
  readonly templateId = input(2);
  readonly sections = input<CvStructuredSection[]>([]);
  readonly editable = input(false);
  readonly compact = input(false);
  readonly profilePhotoUrl = input<string | null>(null);
  readonly inlineEdit = output<unknown>();
}

@Component({
  selector: 'app-cv-export-html-preview',
  standalone: true,
  template: '<div class="stub-html-preview"></div>'
})
class StubCvExportHtmlPreviewComponent {
  readonly srcdoc = input<unknown>(null);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly emptyHint = input('');
  readonly density = input('default');
}

@Component({
  selector: 'app-cv-builder-assist-panel',
  standalone: true,
  template: ''
})
class StubCvBuilderAssistPanelComponent {
  readonly open = input(false);
  readonly sections = input<CvStructuredSection[]>([]);
  readonly aiUpdateSectionIds = input<string[]>([]);
  readonly aiUpdateInstructions = input('');
  readonly summaryProposeInstructions = input('');
  readonly selectedSuggestionIds = input<string[]>([]);
  readonly suggestions = input<unknown[]>([]);
  readonly evaluation = input<unknown>(null);
  readonly summaryProposal = input<unknown>(null);
  readonly updateProposal = input<unknown>(null);
  readonly disabled = input(false);
  readonly proposingUpdate = input(false);
  readonly generatingSuggestions = input(false);
  readonly evaluating = input(false);
  readonly proposing = input(false);
  readonly aiUpdateError = input<string | null>(null);
  readonly suggestionError = input<string | null>(null);
  readonly evaluationError = input<string | null>(null);
  readonly summaryProposalError = input<string | null>(null);
  readonly closePanel = output<void>();
  readonly aiInstructionsChange = output<string>();
  readonly summaryProposeInstructionsChange = output<string>();
  readonly toggleAiSection = output<string>();
  readonly proposeUpdate = output<void>();
  readonly approveUpdateProposal = output<void>();
  readonly discardUpdateProposal = output<void>();
  readonly generateSuggestions = output<void>();
  readonly toggleSuggestion = output<string>();
  readonly applySuggestions = output<void>();
  readonly evaluateQuality = output<void>();
  readonly useFindingInAssist = output<unknown>();
  readonly proposeSummary = output<void>();
  readonly approveSummaryProposal = output<void>();
  readonly discardSummaryProposal = output<void>();
}

describe('CvBuilderPageComponent edit-only canvas (ADR-0003)', () => {
  let fixture: ComponentFixture<CvBuilderPageComponent>;
  let component: CvBuilderPageComponent;
  let refreshExportHtmlPreview: jasmine.Spy;
  let selectedExportTemplateId: ReturnType<typeof signal<number>>;
  let setExportTemplateId: jasmine.Spy;
  let documentSignal: ReturnType<
    typeof signal<{ id: string; hasStructuredContent: boolean; hasProfilePhoto: boolean } | null>
  >;
  let structuredLoadSpy: jasmine.Spy;
  let isSaving: ReturnType<typeof signal<boolean>>;
  let clearAiUpdateError: jasmine.Spy;
  let updateWithAi: jasmine.Spy;

  const sections: CvStructuredSection[] = [
    {
      id: 'sec-1',
      heading: 'Summary',
      sectionType: 'Summary',
      sortOrder: 0,
      entries: [
        {
          id: 'entry-1',
          title: 'Profile',
          subtitle: null,
          dateRange: null,
          summary: 'Hello',
          bullets: [],
          techStack: '',
          fields: {},
          source: 'Manual',
          sourceSummaryId: null,
          sortOrder: 0
        }
      ]
    }
  ];

  beforeEach(async () => {
    refreshExportHtmlPreview = jasmine.createSpy('refreshExportHtmlPreview');
    selectedExportTemplateId = signal(2);
    setExportTemplateId = jasmine
      .createSpy('setExportTemplateId')
      .and.callFake((templateId: number) => selectedExportTemplateId.set(templateId));
    documentSignal = signal({
      id: 'doc-1',
      hasStructuredContent: true,
      hasProfilePhoto: false
    });
    structuredLoadSpy = jasmine.createSpy('load');
    isSaving = signal(false);
    clearAiUpdateError = jasmine.createSpy('clearAiUpdateError');
    updateWithAi = jasmine.createSpy('updateWithAi');

    await TestBed.configureTestingModule({
      imports: [CvBuilderPageComponent],
      providers: [
        CvEditSession,
        {
          provide: CvDocumentFacade,
          useValue: {
            loading: signal(false).asReadonly(),
            document: documentSignal.asReadonly(),
            hasDocument: signal(true).asReadonly(),
            selectedExportTemplateId,
            uploading: signal(false).asReadonly(),
            startingBlank: signal(false).asReadonly(),
            downloadingFormatted: signal(false).asReadonly(),
            uploadingProfilePhoto: signal(false).asReadonly(),
            deletingProfilePhoto: signal(false).asReadonly(),
            loadingProfilePhoto: signal(false).asReadonly(),
            profilePhotoUrl: signal<string | null>(null).asReadonly(),
            profilePhotoError: signal<string | null>(null).asReadonly(),
            uploadError: signal<string | null>(null).asReadonly(),
            downloadFormattedError: signal<string | null>(null).asReadonly(),
            startBlankError: signal<string | null>(null).asReadonly(),
            exportHtmlPreviewSrcdoc: signal<string | null>('<p>export</p>').asReadonly(),
            exportHtmlPreviewLoading: signal(false).asReadonly(),
            exportHtmlPreviewError: signal<string | null>(null).asReadonly(),
            exportHtmlPreviewNotice: signal<string | null>('Compact notice').asReadonly(),
            setExportTemplateId,
            refreshExportHtmlPreview,
            downloadFormattedFile: jasmine.createSpy('downloadFormattedFile'),
            uploadProfilePhoto: jasmine.createSpy('uploadProfilePhoto'),
            deleteProfilePhoto: jasmine.createSpy('deleteProfilePhoto'),
            upload: jasmine.createSpy('upload'),
            startBlankWithStarterSections: jasmine.createSpy('startBlankWithStarterSections')
          }
        },
        {
          provide: CvStructuredFacade,
          useValue: {
            loading: signal(false).asReadonly(),
            structured: signal({ documentId: 'doc-1', structuredImportedAt: null, sections }).asReadonly(),
            isSaving: isSaving.asReadonly(),
            savingSectionId: signal<string | null>(null).asReadonly(),
            updatingWithAi: signal(false).asReadonly(),
            proposingUpdate: signal(false).asReadonly(),
            generatingSuggestions: signal(false).asReadonly(),
            evaluating: signal(false).asReadonly(),
            proposing: signal(false).asReadonly(),
            saveError: signal<string | null>(null).asReadonly(),
            aiUpdateError: signal<string | null>(null).asReadonly(),
            suggestionError: signal<string | null>(null).asReadonly(),
            evaluationError: signal<string | null>(null).asReadonly(),
            summaryProposalError: signal<string | null>(null).asReadonly(),
            suggestions: signal([]).asReadonly(),
            evaluation: signal(null).asReadonly(),
            summaryProposal: signal(null).asReadonly(),
            updateProposal: signal(null).asReadonly(),
            lastSuccessfulSaveGeneration: signal(0).asReadonly(),
            load: structuredLoadSpy,
            save: jasmine.createSpy('save').and.returnValue(1),
            clearSaveError: jasmine.createSpy('clearSaveError'),
            clearAiUpdateError,
            clearSuggestions: jasmine.createSpy('clearSuggestions'),
            clearEvaluation: jasmine.createSpy('clearEvaluation'),
            discardSummaryProposal: jasmine.createSpy('discardSummaryProposal'),
            discardUpdateProposal: jasmine.createSpy('discardUpdateProposal'),
            proposeSummary: jasmine.createSpy('proposeSummary'),
            approveSummaryProposal: jasmine.createSpy('approveSummaryProposal'),
            approveUpdateProposal: jasmine.createSpy('approveUpdateProposal'),
            proposeUpdate: updateWithAi,
            generateSuggestions: jasmine.createSpy('generateSuggestions'),
            evaluateQuality: jasmine.createSpy('evaluateQuality')
          }
        },
        {
          provide: CvProjectsFacade,
          useValue: {
            loadingSummaries: signal(false).asReadonly(),
            savedSummaries: signal([]).asReadonly(),
            summariesError: signal<string | null>(null).asReadonly(),
            loadSummaries: jasmine.createSpy('loadSummaries')
          }
        }
      ]
    })
      .overrideComponent(CvBuilderPageComponent, {
        set: {
          imports: [
            RouterLink,
            StubCvExportTemplatePreviewComponent,
            StubCvBuilderAssistPanelComponent,
            CvBuilderEmptyStartComponent,
            CvBuilderStructurePanelComponent,
            CvBuilderProjectsPanelComponent,
            CvBuilderCheckExportComponent
          ]
        }
      })
      .overrideComponent(CvBuilderCheckExportComponent, {
        set: {
          imports: [StubCvExportHtmlPreviewComponent]
        }
      })
      .compileComponents();

    fixture = TestBed.createComponent(CvBuilderPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders editable template preview on the edit paper (not export HTML iframe)', () => {
    const paper = fixture.debugElement.query(By.css('.cv-builder__paper--editor'));
    expect(paper).withContext('edit paper').not.toBeNull();
    expect(paper.query(By.css('app-cv-export-template-preview'))).not.toBeNull();
    expect(paper.query(By.css('app-cv-export-html-preview'))).toBeNull();
  });

  it('removes Content and Refresh preview topbar controls', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Refresh preview');
    const buttons = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.cv-builder__topbar-right button')
    ).map((btn) => (btn.textContent ?? '').trim());
    expect(buttons).not.toContain('Content');
    expect(buttons).toContain('Check export');
    expect(buttons.indexOf('Check export')).toBeLessThan(buttons.indexOf('Download PDF'));
  });

  it('uses ADR-0003 desk hint and sync chip without Updating preview…', () => {
    const hint = (fixture.nativeElement as HTMLElement).querySelector('.cv-builder__desk-hint');
    expect(hint?.textContent).toContain('Edit on the canvas');
    expect(hint?.textContent).toContain('Check export / Download PDF');
    expect((component as unknown as { saveStatus: () => string }).saveStatus()).toBe('Saved');
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Updating preview…');
  });

  it('opens Check export modal and fetches export HTML on demand', () => {
    const trigger = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button')
    ).find((btn) => (btn.textContent ?? '').trim() === 'Check export') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();

    expect(refreshExportHtmlPreview).toHaveBeenCalled();
    const dialog = fixture.nativeElement.querySelector('#cv-builder-check-export-dialog');
    expect(dialog).not.toBeNull();
    expect(dialog.getAttribute('role')).toBe('dialog');
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(dialog.textContent).toContain('approximation');
    expect(dialog.textContent).toContain('Compact notice');
    expect(dialog.querySelector('app-cv-export-html-preview')).not.toBeNull();
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('.cv-builder__desk-scroll')?.textContent
    ).not.toContain('Compact notice');
  });

  it('refreshes export HTML from in-modal Refresh only', () => {
    (component as unknown as { openCheckExport: () => void }).openCheckExport();
    fixture.detectChanges();
    refreshExportHtmlPreview.calls.reset();

    const refresh = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll(
        '#cv-builder-check-export-dialog button'
      )
    ).find((btn) => (btn.textContent ?? '').trim() === 'Refresh') as HTMLButtonElement;
    refresh.click();

    expect(refreshExportHtmlPreview).toHaveBeenCalledTimes(1);
  });

  it('uses exclusive activePanel mutex for Structure / Projects / Assist / Check export', () => {
    const page = component as unknown as {
      activePanel: () => string | null;
      openAssist: () => void;
      toggleStructure: () => void;
      toggleProjects: () => void;
      openCheckExport: () => void;
      closeCheckExport: () => void;
    };

    page.openAssist();
    expect(page.activePanel()).toBe('assist');

    page.toggleStructure();
    expect(page.activePanel()).toBe('structure');

    page.toggleProjects();
    expect(page.activePanel()).toBe('projects');

    page.openCheckExport();
    expect(page.activePanel()).toBe('checkExport');

    page.closeCheckExport();
    expect(page.activePanel()).toBeNull();
  });

  it('updates edit canvas templateId when topbar Template select changes (2 → 3)', () => {
    const select = (fixture.nativeElement as HTMLElement).querySelector(
      '.cv-builder__topbar select.cv-builder__select'
    ) as HTMLSelectElement;
    expect(select).withContext('topbar template select').not.toBeNull();

    const previewDe = fixture.debugElement.query(By.css('.cv-builder__paper--editor app-cv-export-template-preview'));
    expect(previewDe).not.toBeNull();
    const preview = previewDe.componentInstance as StubCvExportTemplatePreviewComponent;

    expect(preview.templateId()).toBe(2);

    for (const templateId of [3, 2] as const) {
      select.value = String(templateId);
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();

      expect(setExportTemplateId).toHaveBeenCalledWith(templateId);
      expect(selectedExportTemplateId()).toBe(templateId);
      expect(preview.templateId())
        .withContext(`canvas templateId after selecting ${templateId}`)
        .toBe(templateId);
      expect(select.value).toBe(String(templateId));
      const selectedOption = Array.from(select.options).find((option) => option.selected);
      expect(selectedOption?.value)
        .withContext(`selected option after choosing ${templateId}`)
        .toBe(String(templateId));
    }
  });

  it('loads structured only when document id changes (prefs echo reload gate)', () => {
    expect(structuredLoadSpy).toHaveBeenCalledTimes(1);

    // Same document id with metadata echo (export prefs) must not reload structured.
    documentSignal.set({
      id: 'doc-1',
      hasStructuredContent: true,
      hasProfilePhoto: true
    });
    fixture.detectChanges();
    TestBed.flushEffects();

    expect(structuredLoadSpy).toHaveBeenCalledTimes(1);

    documentSignal.set({
      id: 'doc-2',
      hasStructuredContent: true,
      hasProfilePhoto: false
    });
    fixture.detectChanges();
    TestBed.flushEffects();

    expect(structuredLoadSpy).toHaveBeenCalledTimes(2);
  });

  it('useFindingInAssist copies title+detail into instructions and focuses section chip (D5)', () => {
    const page = component as unknown as {
      aiUpdateInstructions: () => string;
      aiUpdateSectionIds: () => string[];
      useFindingInAssist: (finding: {
        id: string;
        dimension: string;
        severity: string;
        title: string;
        detail: string;
        sectionId: string | null;
        entryId: string | null;
      }) => void;
    };

    page.useFindingInAssist({
      id: 'f-1',
      dimension: 'content',
      severity: 'warning',
      title: 'Weak summary',
      detail: 'Add concrete outcomes to the summary.',
      sectionId: 'sec-1',
      entryId: null
    });

    expect(page.aiUpdateInstructions()).toContain('Weak summary');
    expect(page.aiUpdateInstructions()).toContain('Add concrete outcomes to the summary.');
    expect(page.aiUpdateInstructions()).toContain('Severity: warning');
    expect(page.aiUpdateInstructions()).toContain('Dimension: content');
    expect(page.aiUpdateSectionIds()).toEqual(['sec-1']);
    expect(clearAiUpdateError).toHaveBeenCalled();
    expect(updateWithAi).not.toHaveBeenCalled();
  });

  it('useFindingInAssist leaves section chips unchanged when sectionId is missing or invalid', () => {
    const page = component as unknown as {
      aiUpdateInstructions: (() => string) & { set: (value: string) => void };
      aiUpdateSectionIds: (() => string[]) & { set: (value: string[]) => void };
      useFindingInAssist: (finding: {
        id: string;
        dimension: string;
        severity: string;
        title: string;
        detail: string;
        sectionId: string | null;
        entryId: string | null;
      }) => void;
    };

    page.aiUpdateSectionIds.set(['sec-1']);
    page.aiUpdateInstructions.set('prior notes');

    page.useFindingInAssist({
      id: 'f-2',
      dimension: 'structure',
      severity: 'info',
      title: 'Add skills coverage',
      detail: 'Consider a dedicated Skills section.',
      sectionId: 'missing-section',
      entryId: null
    });

    expect(page.aiUpdateInstructions()).toContain('Add skills coverage');
    expect(page.aiUpdateSectionIds()).toEqual(['sec-1']);
    expect(updateWithAi).not.toHaveBeenCalled();
  });
});
