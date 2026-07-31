import { Component, input, output, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';

import { CvDocumentFacade } from '../../data-access/cv-document.facade';
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
  readonly sampleMode = input(false);
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
  readonly selectedSuggestionIds = input<string[]>([]);
  readonly suggestions = input<unknown[]>([]);
  readonly disabled = input(false);
  readonly closePanel = output<void>();
  readonly aiInstructionsChange = output<string>();
  readonly toggleAiSection = output<string>();
  readonly updateWithAi = output<void>();
  readonly generateSuggestions = output<void>();
  readonly toggleSuggestion = output<string>();
  readonly applySuggestions = output<void>();
}

describe('CvBuilderPageComponent edit-only canvas (ADR-0003)', () => {
  let fixture: ComponentFixture<CvBuilderPageComponent>;
  let component: CvBuilderPageComponent;
  let refreshExportHtmlPreview: jasmine.Spy;
  let selectedExportTemplateId: ReturnType<typeof signal<number>>;
  let setExportTemplateId: jasmine.Spy;

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

    await TestBed.configureTestingModule({
      imports: [CvBuilderPageComponent],
      providers: [
        {
          provide: CvDocumentFacade,
          useValue: {
            loading: signal(false).asReadonly(),
            document: signal({
              id: 'doc-1',
              hasStructuredContent: true,
              hasProfilePhoto: false
            }).asReadonly(),
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
            isSaving: signal(false).asReadonly(),
            savingSectionId: signal<string | null>(null).asReadonly(),
            savingSectionOrder: signal(false).asReadonly(),
            updatingWithAi: signal(false).asReadonly(),
            saveError: signal<string | null>(null).asReadonly(),
            aiUpdateError: signal<string | null>(null).asReadonly(),
            suggestions: signal([]).asReadonly(),
            lastSuccessfulSaveGeneration: signal(0).asReadonly(),
            load: jasmine.createSpy('load'),
            save: jasmine.createSpy('save').and.returnValue(1),
            clearSaveError: jasmine.createSpy('clearSaveError'),
            clearAiUpdateError: jasmine.createSpy('clearAiUpdateError'),
            clearSuggestions: jasmine.createSpy('clearSuggestions'),
            updateWithAi: jasmine.createSpy('updateWithAi'),
            generateSuggestions: jasmine.createSpy('generateSuggestions')
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
            StubCvExportHtmlPreviewComponent,
            StubCvBuilderAssistPanelComponent
          ]
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
});
