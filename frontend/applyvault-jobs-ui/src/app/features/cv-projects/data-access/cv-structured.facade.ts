import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { isRequestAborted } from '../../../core/http/is-request-aborted';
import {
  CvStructuredDocument,
  CvStructuredEntry,
  CvStructuredEntryWrite,
  CvStructuredImportPreview,
  CvStructuredSection,
  CvStructuredSectionWrite,
  SaveCvStructuredDocumentRequest
} from '../models/cv-structured.model';
import { CvDocumentApiService } from './cv-document-api.service';

@Injectable({ providedIn: 'root' })
export class CvStructuredFacade {
  private readonly apiService = inject(CvDocumentApiService);
  private loadSubscription: Subscription | null = null;
  private saveSubscription: Subscription | null = null;
  private importSubscription: Subscription | null = null;
  private confirmSubscription: Subscription | null = null;
  private insertSubscription: Subscription | null = null;

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly importing = signal(false);
  readonly confirmingImport = signal(false);
  readonly inserting = signal(false);
  readonly structured = signal<CvStructuredDocument | null>(null);
  readonly importPreview = signal<CvStructuredImportPreview | null>(null);
  readonly error = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly importError = signal<string | null>(null);
  readonly insertError = signal<string | null>(null);

  load(): void {
    this.cancelLoad();
    this.loading.set(true);
    this.error.set(null);

    this.loadSubscription = this.apiService.getStructured().subscribe({
      next: (document) => {
        this.loading.set(false);
        this.structured.set(document);
      },
      error: (error) => {
        this.loading.set(false);

        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.structured.set(null);
          return;
        }

        if (isRequestAborted(error)) {
          return;
        }

        this.error.set(this.readErrorMessage(error, 'Could not load structured CV content.'));
      }
    });
  }

  save(request: SaveCvStructuredDocumentRequest): void {
    this.cancelSave();
    this.saving.set(true);
    this.saveError.set(null);

    this.saveSubscription = this.apiService.saveStructured(request).subscribe({
      next: (document) => {
        this.saving.set(false);
        this.structured.set(document);
      },
      error: (error) => {
        this.saving.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.saveError.set(this.readErrorMessage(error, 'Could not save CV content.'));
      }
    });
  }

  previewImport(): void {
    this.cancelImport();
    this.importing.set(true);
    this.importError.set(null);

    this.importSubscription = this.apiService.previewImport().subscribe({
      next: (preview) => {
        this.importing.set(false);
        this.importPreview.set(preview);
      },
      error: (error) => {
        this.importing.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.importError.set(this.readErrorMessage(error, 'Could not extract content from your CV PDF.'));
      }
    });
  }

  confirmImport(request: SaveCvStructuredDocumentRequest): void {
    this.cancelConfirm();
    this.confirmingImport.set(true);
    this.importError.set(null);

    this.confirmSubscription = this.apiService.confirmImport(request).subscribe({
      next: (document) => {
        this.confirmingImport.set(false);
        this.importPreview.set(null);
        this.structured.set(document);
      },
      error: (error) => {
        this.confirmingImport.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.importError.set(this.readErrorMessage(error, 'Could not save imported CV content.'));
      }
    });
  }

  insertFromSummary(
    sectionId: string,
    summaryId: string,
    onSuccess: (entry: CvStructuredEntry) => void
  ): void {
    this.cancelInsert();
    this.inserting.set(true);
    this.insertError.set(null);

    this.insertSubscription = this.apiService.insertEntryFromSummary(sectionId, { summaryId }).subscribe({
      next: (entry) => {
        this.inserting.set(false);
        onSuccess(entry);
      },
      error: (error) => {
        this.inserting.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.insertError.set(this.readErrorMessage(error, 'Could not insert project summary.'));
      }
    });
  }

  clearImportPreview(): void {
    this.importPreview.set(null);
  }

  static toWriteRequest(sections: readonly CvStructuredSection[]): SaveCvStructuredDocumentRequest {
    return {
      sections: sections.map((section, sectionIndex) => ({
        id: section.id,
        heading: section.heading,
        sectionType: section.sectionType,
        sortOrder: sectionIndex,
        entries: section.entries.map((entry, entryIndex) => ({
          id: entry.id,
          title: entry.title,
          subtitle: entry.subtitle,
          dateRange: entry.dateRange,
          summary: entry.summary,
          bullets: [...entry.bullets],
          techStack: entry.techStack,
          source: entry.source,
          sourceSummaryId: entry.sourceSummaryId,
          sortOrder: entryIndex
        }))
      }))
    };
  }

  static previewToSections(preview: CvStructuredImportPreview): CvStructuredSection[] {
    return preview.sections.map((section, sectionIndex) => ({
      id: crypto.randomUUID(),
      heading: section.heading,
      sectionType: section.sectionType,
      sortOrder: sectionIndex,
      entries: section.entries.map((entry, entryIndex) => ({
        id: crypto.randomUUID(),
        title: entry.title,
        subtitle: entry.subtitle,
        dateRange: entry.dateRange,
        summary: entry.summary,
        bullets: [...entry.bullets],
        techStack: entry.techStack,
        source: entry.source,
        sourceSummaryId: entry.sourceSummaryId,
        sortOrder: entryIndex
      }))
    }));
  }

  static previewToWriteRequest(preview: CvStructuredImportPreview): SaveCvStructuredDocumentRequest {
    return {
      sections: preview.sections.map((section, sectionIndex) => ({
        id: section.id ?? null,
        heading: section.heading,
        sectionType: section.sectionType,
        sortOrder: sectionIndex,
        entries: section.entries.map((entry, entryIndex) => ({
          id: entry.id ?? null,
          title: entry.title,
          subtitle: entry.subtitle,
          dateRange: entry.dateRange,
          summary: entry.summary,
          bullets: [...entry.bullets],
          techStack: entry.techStack,
          source: entry.source,
          sourceSummaryId: entry.sourceSummaryId,
          sortOrder: entryIndex
        }))
      }))
    };
  }

  private cancelLoad(): void {
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = null;
  }

  private cancelSave(): void {
    this.saveSubscription?.unsubscribe();
    this.saveSubscription = null;
  }

  private cancelImport(): void {
    this.importSubscription?.unsubscribe();
    this.importSubscription = null;
  }

  private cancelConfirm(): void {
    this.confirmSubscription?.unsubscribe();
    this.confirmSubscription = null;
  }

  private cancelInsert(): void {
    this.insertSubscription?.unsubscribe();
    this.insertSubscription = null;
  }

  private readErrorMessage(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const payload = (error as { error: unknown }).error;

      if (typeof payload === 'string' && payload.trim()) {
        return payload;
      }
    }

    return fallback;
  }
}
