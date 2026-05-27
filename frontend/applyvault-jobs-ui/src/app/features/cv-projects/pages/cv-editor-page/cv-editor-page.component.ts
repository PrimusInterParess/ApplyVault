import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import { CvDocumentFacade } from '../../data-access/cv-document.facade';
import { CvStructuredFacade } from '../../data-access/cv-structured.facade';
import { CvProjectsApiService } from '../../data-access/cv-projects-api.service';
import {
  CV_SECTION_TYPES,
  CvSectionType,
  CvStructuredEntry,
  CvStructuredSection
} from '../../models/cv-structured.model';
import { CvProjectSummary } from '../../models/cv-project.model';

@Component({
  selector: 'app-cv-editor-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, SkeletonBlockComponent],
  templateUrl: './cv-editor-page.component.html',
  styleUrl: './cv-editor-page.component.scss'
})
export class CvEditorPageComponent implements OnInit {
  protected readonly cvDocument = inject(CvDocumentFacade);
  protected readonly cvStructured = inject(CvStructuredFacade);
  private readonly cvProjectsApi = inject(CvProjectsApiService);

  protected readonly sectionTypes = CV_SECTION_TYPES;
  protected readonly draftSections = signal<readonly CvStructuredSection[]>([]);
  protected readonly importReviewOpen = signal(false);
  protected readonly insertDialogOpen = signal(false);
  protected readonly insertSectionId = signal<string | null>(null);
  protected readonly selectedSectionId = signal<string | null>(null);
  protected readonly projectSummaries = signal<readonly CvProjectSummary[]>([]);
  protected readonly loadingSummaries = signal(false);

  protected readonly hasDraft = computed(() => this.draftSections().length > 0);
  protected readonly selectedSection = computed(() => {
    const sections = this.draftSections();
    const selectedId = this.selectedSectionId();

    return sections.find((section) => section.id === selectedId) ?? sections[0] ?? null;
  });
  protected readonly canSave = computed(
    () => this.cvDocument.hasDocument() && this.draftSections().length > 0 && !this.cvStructured.saving()
  );

  private readonly structuredSnapshot = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (!this.cvDocument.loading() && this.cvDocument.hasDocument()) {
        this.cvStructured.load();
      }
    });

    effect(() => {
      const document = this.cvStructured.structured();

      if (!document || this.cvStructured.loading()) {
        return;
      }

      const snapshot = JSON.stringify(document.sections.map((section) => section.id));

      if (snapshot === this.structuredSnapshot()) {
        return;
      }

      this.structuredSnapshot.set(snapshot);
      this.draftSections.set(this.cloneSections(document.sections));
      this.ensureSelectedSection();
    });
  }

  ngOnInit(): void {
    this.loadProjectSummaries();
  }

  protected startImport(): void {
    this.cvStructured.previewImport();
    this.importReviewOpen.set(true);
  }

  protected cancelImportReview(): void {
    this.importReviewOpen.set(false);
    this.cvStructured.clearImportPreview();
  }

  protected applyImportPreview(): void {
    const preview = this.cvStructured.importPreview();

    if (!preview) {
      return;
    }

    this.draftSections.set(CvStructuredFacade.previewToSections(preview));
    this.ensureSelectedSection();
    this.importReviewOpen.set(false);
    this.cvStructured.clearImportPreview();
  }

  protected confirmImportAndSave(): void {
    const preview = this.cvStructured.importPreview();

    if (!preview) {
      return;
    }

    this.cvStructured.confirmImport(CvStructuredFacade.previewToWriteRequest(preview));
    this.importReviewOpen.set(false);
  }

  protected saveDraft(): void {
    this.cvStructured.save(CvStructuredFacade.toWriteRequest(this.draftSections()));
  }

  protected exportPdf(): void {
    this.cvDocument.exportStructured();
  }

  protected addSection(): void {
    const sections = [...this.draftSections()];
    const section: CvStructuredSection = {
      id: crypto.randomUUID(),
      heading: 'New section',
      sectionType: 'Custom',
      sortOrder: sections.length,
      entries: []
    };

    sections.push(section);
    this.draftSections.set(this.normalizeSortOrders(sections));
    this.selectedSectionId.set(section.id);
  }

  protected removeSection(sectionId: string): void {
    const sections = this.normalizeSortOrders(this.draftSections().filter((s) => s.id !== sectionId));

    this.draftSections.set(sections);

    if (this.selectedSectionId() === sectionId) {
      this.selectedSectionId.set(sections[0]?.id ?? null);
    }
  }

  protected moveSection(sectionId: string, delta: number): void {
    const sections = [...this.draftSections()];
    const index = sections.findIndex((s) => s.id === sectionId);

    if (index < 0) {
      return;
    }

    const target = index + delta;

    if (target < 0 || target >= sections.length) {
      return;
    }

    [sections[index], sections[target]] = [sections[target], sections[index]];
    this.draftSections.set(this.normalizeSortOrders(sections));
  }

  protected updateSectionHeading(sectionId: string, heading: string): void {
    this.patchSection(sectionId, (section) => ({ ...section, heading }));
  }

  protected updateSectionType(sectionId: string, sectionType: CvSectionType): void {
    this.patchSection(sectionId, (section) => ({ ...section, sectionType }));
  }

  protected selectSection(sectionId: string): void {
    this.selectedSectionId.set(sectionId);
  }

  protected addEntry(sectionId: string): void {
    this.patchSection(sectionId, (section) => ({
      ...section,
      entries: [
        ...section.entries,
        {
          id: crypto.randomUUID(),
          title: '',
          subtitle: null,
          dateRange: null,
          summary: '',
          bullets: [],
          techStack: '',
          source: 'Manual',
          sourceSummaryId: null,
          sortOrder: section.entries.length
        }
      ]
    }));
  }

  protected removeEntry(sectionId: string, entryId: string): void {
    this.patchSection(sectionId, (section) => ({
      ...section,
      entries: section.entries.filter((entry) => entry.id !== entryId)
    }));
  }

  protected moveEntry(sectionId: string, entryId: string, delta: number): void {
    this.patchSection(sectionId, (section) => {
      const entries = [...section.entries];
      const index = entries.findIndex((e) => e.id === entryId);

      if (index < 0) {
        return section;
      }

      const target = index + delta;

      if (target < 0 || target >= entries.length) {
        return section;
      }

      [entries[index], entries[target]] = [entries[target], entries[index]];

      return {
        ...section,
        entries: entries.map((entry, entryIndex) => ({ ...entry, sortOrder: entryIndex }))
      };
    });
  }

  protected updateEntryField(
    sectionId: string,
    entryId: string,
    field: keyof CvStructuredEntry,
    value: string
  ): void {
    this.patchSection(sectionId, (section) => ({
      ...section,
      entries: section.entries.map((entry) => {
        if (entry.id !== entryId) {
          return entry;
        }

        if (field === 'bullets') {
          return {
            ...entry,
            bullets: value
              .split('\n')
              .map((line) => line.trim())
              .filter((line) => line.length > 0)
          };
        }

        return { ...entry, [field]: value };
      })
    }));
  }

  protected bulletsText(entry: CvStructuredEntry): string {
    return entry.bullets.join('\n');
  }

  protected openInsertDialog(sectionId: string): void {
    this.insertSectionId.set(sectionId);
    this.insertDialogOpen.set(true);
  }

  protected closeInsertDialog(): void {
    this.insertDialogOpen.set(false);
    this.insertSectionId.set(null);
  }

  protected insertSummary(summaryId: string): void {
    const sectionId = this.insertSectionId();

    if (!sectionId) {
      return;
    }

    const insert = (): void => {
      this.cvStructured.insertFromSummary(sectionId, summaryId, (entry) => {
        this.patchSection(sectionId, (section) => ({
          ...section,
          entries: [...section.entries, entry]
        }));
        this.closeInsertDialog();
      });
    };

    if (this.cvStructured.saving()) {
      return;
    }

    this.cvStructured.save(CvStructuredFacade.toWriteRequest(this.draftSections()));

    const watch = setInterval(() => {
      if (!this.cvStructured.saving()) {
        clearInterval(watch);

        if (!this.cvStructured.saveError()) {
          insert();
        }
      }
    }, 50);
  }

  private loadProjectSummaries(): void {
    this.loadingSummaries.set(true);
    this.cvProjectsApi.listAllSummaries().subscribe({
      next: (summaries) => {
        this.loadingSummaries.set(false);
        this.projectSummaries.set(summaries);
      },
      error: () => {
        this.loadingSummaries.set(false);
        this.projectSummaries.set([]);
      }
    });
  }

  private patchSection(
    sectionId: string,
    updater: (section: CvStructuredSection) => CvStructuredSection
  ): void {
    this.draftSections.set(
      this.draftSections().map((section) => (section.id === sectionId ? updater(section) : section))
    );
  }

  private ensureSelectedSection(): void {
    const sections = this.draftSections();

    if (sections.length === 0) {
      this.selectedSectionId.set(null);
      return;
    }

    if (!sections.some((section) => section.id === this.selectedSectionId())) {
      this.selectedSectionId.set(sections[0].id);
    }
  }

  private normalizeSortOrders(sections: CvStructuredSection[]): CvStructuredSection[] {
    return sections.map((section, sectionIndex) => ({
      ...section,
      sortOrder: sectionIndex,
      entries: section.entries.map((entry, entryIndex) => ({ ...entry, sortOrder: entryIndex }))
    }));
  }

  private cloneSections(sections: readonly CvStructuredSection[]): CvStructuredSection[] {
    return sections.map((section) => ({
      ...section,
      entries: section.entries.map((entry) => ({
        ...entry,
        bullets: [...entry.bullets]
      }))
    }));
  }
}
