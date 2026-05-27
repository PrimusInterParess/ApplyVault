import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, effect, inject, OnInit, signal, viewChild, ElementRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import { CvDocumentFacade } from '../../data-access/cv-document.facade';
import { CvProjectsFacade } from '../../data-access/cv-projects.facade';
import { CvProjectSummary, CvProjectSummaryPlacement, CvPdfSection } from '../../models/cv-project.model';

interface MergeDraftRow {
  readonly summary: CvProjectSummary;
  includeInMerge: boolean;
  mergeSectionHeading: string | null;
  mergeSortOrder: number;
}

const APPENDIX_SECTION_VALUE = '';

@Component({
  selector: 'app-my-cv-page',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, RouterLink, SkeletonBlockComponent],
  templateUrl: './my-cv-page.component.html',
  styleUrl: './my-cv-page.component.scss'
})
export class MyCvPageComponent implements OnInit {
  protected readonly cvDocument = inject(CvDocumentFacade);
  protected readonly cvProjects = inject(CvProjectsFacade);
  protected readonly deleteConfirmOpen = signal(false);
  protected readonly mergeConfigOpen = signal(false);
  protected readonly mergeDraftRows = signal<readonly MergeDraftRow[]>([]);
  protected readonly cvFileInput = viewChild<ElementRef<HTMLInputElement>>('cvFileInput');

  protected readonly appendixSectionValue = APPENDIX_SECTION_VALUE;

  protected readonly includedMergeCount = computed(
    () => this.mergeDraftRows().filter((row) => row.includeInMerge).length
  );

  protected readonly canMergeProjects = computed(
    () =>
      this.cvDocument.hasDocument() &&
      this.mergeDraftRows().some((row) => row.includeInMerge)
  );

  protected readonly unmatchedSectionWarnings = computed(() => {
    const detectedHeadings = new Set(
      this.cvProjects.cvSections().map((section) => section.headingText.toLowerCase())
    );

    return this.mergeDraftRows()
      .filter(
        (row) =>
          row.includeInMerge &&
          row.mergeSectionHeading &&
          !detectedHeadings.has(row.mergeSectionHeading.toLowerCase())
      )
      .map((row) => row.summary.cvTitle);
  });

  constructor() {
    effect(() => {
      if (this.cvDocument.hasDocument()) {
        this.cvProjects.loadMergeConfig();
        this.cvProjects.loadCvSections();
      }
    });

    effect(() => {
      const summaries = this.cvProjects.mergeSummaries();

      this.mergeDraftRows.set(
        summaries.map((summary) => ({
          summary,
          includeInMerge: summary.includeInMerge,
          mergeSectionHeading: summary.mergeSectionHeading,
          mergeSortOrder: summary.mergeSortOrder
        }))
      );
    });
  }

  ngOnInit(): void {
    this.cvProjects.loadSummaries();
  }

  protected mergeProjectsLabel(): string {
    return this.cvDocument.document()?.hasMergedProjects
      ? 'Regenerate CV with projects'
      : 'Add projects to CV';
  }

  protected toggleMergeConfig(): void {
    this.mergeConfigOpen.update((open) => !open);
  }

  protected sectionOptions(): readonly CvPdfSection[] {
    return this.cvProjects.cvSections();
  }

  protected rowsForSection(sectionHeading: string | null): readonly MergeDraftRow[] {
    return this.mergeDraftRows()
      .filter((row) => (row.mergeSectionHeading ?? APPENDIX_SECTION_VALUE) === (sectionHeading ?? APPENDIX_SECTION_VALUE))
      .sort((left, right) => left.mergeSortOrder - right.mergeSortOrder);
  }

  protected allDraftRowsOrdered(): readonly MergeDraftRow[] {
    return [...this.mergeDraftRows()].sort((left, right) => {
      const sectionCompare = (left.mergeSectionHeading ?? '').localeCompare(right.mergeSectionHeading ?? '');

      if (sectionCompare !== 0) {
        return sectionCompare;
      }

      return left.mergeSortOrder - right.mergeSortOrder;
    });
  }

  protected setInclude(row: MergeDraftRow, includeInMerge: boolean): void {
    this.mergeDraftRows.update((rows) =>
      rows.map((current) =>
        current.summary.id === row.summary.id ? { ...current, includeInMerge } : current
      )
    );
  }

  protected setSection(row: MergeDraftRow, mergeSectionHeading: string): void {
    const normalizedHeading = mergeSectionHeading.trim() ? mergeSectionHeading : null;
    const nextSortOrder = this.rowsForSection(normalizedHeading).length;

    this.mergeDraftRows.update((rows) =>
      rows.map((current) =>
        current.summary.id === row.summary.id
          ? { ...current, mergeSectionHeading: normalizedHeading, mergeSortOrder: nextSortOrder }
          : current
      )
    );

    this.normalizeSortOrders();
  }

  protected canMoveRow(row: MergeDraftRow, direction: -1 | 1): boolean {
    const sectionRows = this.rowsForSection(row.mergeSectionHeading);
    const currentIndex = sectionRows.findIndex((item) => item.summary.id === row.summary.id);

    if (currentIndex < 0) {
      return false;
    }

    const targetIndex = currentIndex + direction;
    return targetIndex >= 0 && targetIndex < sectionRows.length;
  }

  protected moveRow(row: MergeDraftRow, direction: -1 | 1): void {
    const sectionRows = [...this.rowsForSection(row.mergeSectionHeading)];
    const currentIndex = sectionRows.findIndex((item) => item.summary.id === row.summary.id);

    if (currentIndex < 0) {
      return;
    }

    const targetIndex = currentIndex + direction;

    if (targetIndex < 0 || targetIndex >= sectionRows.length) {
      return;
    }

    [sectionRows[currentIndex], sectionRows[targetIndex]] = [
      sectionRows[targetIndex],
      sectionRows[currentIndex]
    ];

    this.mergeDraftRows.update((rows) =>
      rows.map((current) => {
        const updatedIndex = sectionRows.findIndex((item) => item.summary.id === current.summary.id);

        if (updatedIndex < 0) {
          return current;
        }

        return { ...current, mergeSortOrder: updatedIndex };
      })
    );
  }

  protected savePlacements(): void {
    const placements: CvProjectSummaryPlacement[] = this.allDraftRowsOrdered().map((row, index) => ({
      summaryId: row.summary.id,
      includeInMerge: row.includeInMerge,
      mergeSectionHeading: row.mergeSectionHeading,
      mergeSortOrder: row.mergeSortOrder
    }));

    this.cvProjects.savePlacements(placements);
  }

  protected mergeProjects(): void {
    const placements: CvProjectSummaryPlacement[] = this.allDraftRowsOrdered().map((row) => ({
      summaryId: row.summary.id,
      includeInMerge: row.includeInMerge,
      mergeSectionHeading: row.mergeSectionHeading,
      mergeSortOrder: row.mergeSortOrder
    }));

    this.cvProjects.savePlacements(placements, () => this.cvDocument.mergeProjects());
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

  private normalizeSortOrders(): void {
    const grouped = new Map<string | null, MergeDraftRow[]>();

    for (const row of this.mergeDraftRows()) {
      const key = row.mergeSectionHeading;
      const bucket = grouped.get(key) ?? [];
      bucket.push(row);
      grouped.set(key, bucket);
    }

    this.mergeDraftRows.update((rows) =>
      rows.map((row) => {
        const bucket = grouped.get(row.mergeSectionHeading) ?? [];
        const index = bucket.findIndex((item) => item.summary.id === row.summary.id);

        return index >= 0 ? { ...row, mergeSortOrder: index } : row;
      })
    );
  }
}
