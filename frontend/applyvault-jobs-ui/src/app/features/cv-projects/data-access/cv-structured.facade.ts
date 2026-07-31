import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { isRequestAborted } from '../../../core/http/is-request-aborted';
import {
  CvImprovementSuggestion,
  CvStructuredDocument,
  CvStructuredSection
} from '../models/cv-structured.model';
import { toSaveRequest, hydrateStructuredDocument } from '../utils/cv-structured-draft.util';
import { normalizeSectionsForEditing } from '../utils/cv-structured-edit-normalizer.util';
import { CvDocumentApiService } from './cv-document-api.service';

type PendingStructuredSave = {
  sections: readonly CvStructuredSection[];
  sectionId: string | null;
  orderOnly: boolean;
  generation: number;
};

@Injectable({ providedIn: 'root' })
export class CvStructuredFacade {
  private readonly apiService = inject(CvDocumentApiService);
  private loadSubscription: Subscription | null = null;
  private saveSubscription: Subscription | null = null;
  private aiUpdateSubscription: Subscription | null = null;
  private suggestionsSubscription: Subscription | null = null;

  private loadGeneration = 0;
  private saveGeneration = 0;
  private pendingSave: PendingStructuredSave | null = null;

  readonly loading = signal(false);
  readonly savingSectionId = signal<string | null>(null);
  readonly savingSectionOrder = signal(false);
  readonly updatingWithAi = signal(false);
  readonly generatingSuggestions = signal(false);
  readonly structured = signal<CvStructuredDocument | null>(null);
  readonly suggestions = signal<CvImprovementSuggestion[]>([]);
  readonly error = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly aiUpdateError = signal<string | null>(null);
  readonly suggestionError = signal<string | null>(null);
  /** Monotonic generation of the last successfully applied structured save. */
  readonly lastSuccessfulSaveGeneration = signal(0);

  readonly isSaving = computed(
    () => this.savingSectionId() !== null || this.savingSectionOrder()
  );

  load(): void {
    const generation = ++this.loadGeneration;
    this.cancelLoadSubscription();
    this.loading.set(true);
    this.error.set(null);

    this.loadSubscription = this.apiService.getStructured().subscribe({
      next: (document) => {
        if (generation !== this.loadGeneration) {
          return;
        }

        this.loading.set(false);
        this.structured.set(this.hydrateForContentEditing(document));
      },
      error: (error) => {
        if (generation !== this.loadGeneration) {
          return;
        }

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

  /**
   * Persist sections. Coalesces overlapping PUTs to the latest payload (never cancels an
   * in-flight HTTP PUT — unsubscribe would not cancel the server). Returns the save generation.
   */
  save(sections: readonly CvStructuredSection[], sectionId: string): number {
    const generation = ++this.saveGeneration;
    this.saveError.set(null);
    this.enqueueOrStartSave({
      sections,
      sectionId,
      orderOnly: false,
      generation
    });
    return generation;
  }

  saveSectionOrder(sections: readonly CvStructuredSection[]): number {
    const generation = ++this.saveGeneration;
    this.saveError.set(null);
    this.enqueueOrStartSave({
      sections,
      sectionId: null,
      orderOnly: true,
      generation
    });
    return generation;
  }

  updateWithAi(instructions: string, sectionIds?: readonly string[]): void {
    const trimmedInstructions = instructions.trim();

    if (!trimmedInstructions || this.updatingWithAi()) {
      return;
    }

    this.cancelAiUpdate();
    this.updatingWithAi.set(true);
    this.aiUpdateError.set(null);

    this.aiUpdateSubscription = this.apiService
      .updateStructuredWithAi(trimmedInstructions, sectionIds)
      .subscribe({
        next: (document) => {
          this.updatingWithAi.set(false);
          this.structured.set(this.hydrateForContentEditing(document));
        },
        error: (error) => {
          this.updatingWithAi.set(false);

          if (isRequestAborted(error)) {
            return;
          }

          this.aiUpdateError.set(
            this.readErrorMessage(error, 'Could not update structured CV content with AI.')
          );
        }
      });
  }

  generateSuggestions(sectionIds?: readonly string[], maxSuggestions = 6): void {
    if (this.generatingSuggestions()) {
      return;
    }

    this.cancelSuggestions();
    this.generatingSuggestions.set(true);
    this.suggestionError.set(null);

    this.suggestionsSubscription = this.apiService
      .generateStructuredSuggestions(sectionIds, maxSuggestions)
      .subscribe({
        next: (result) => {
          this.generatingSuggestions.set(false);
          this.suggestions.set(result.suggestions);
        },
        error: (error) => {
          this.generatingSuggestions.set(false);

          if (isRequestAborted(error)) {
            return;
          }

          this.suggestionError.set(
            this.readErrorMessage(error, 'Could not generate CV improvement suggestions.')
          );
        }
      });
  }

  clearSaveError(): void {
    this.saveError.set(null);
  }

  clearAiUpdateError(): void {
    this.aiUpdateError.set(null);
  }

  clearSuggestionError(): void {
    this.suggestionError.set(null);
  }

  clearSuggestions(): void {
    this.suggestions.set([]);
    this.suggestionError.set(null);
  }

  setStructured(document: CvStructuredDocument): void {
    this.structured.set(this.hydrateForContentEditing(document));
  }

  /**
   * Hydrate API payload and normalize edit slots (Summary/Skills/Contact),
   * including Contact modern expand + absorb/dedupe so Minimal/Modern canvases
   * never show unlabeled orphans beside empty Email/Phone/LinkedIn starters.
   */
  private hydrateForContentEditing(document: CvStructuredDocument): CvStructuredDocument {
    const hydrated = hydrateStructuredDocument(document);

    return {
      ...hydrated,
      // Full Contact modernize + absorb/dedupe (not only per-entry summary→bullets).
      sections: normalizeSectionsForEditing(hydrated.sections)
    };
  }

  private enqueueOrStartSave(request: PendingStructuredSave): void {
    if (this.saveSubscription) {
      // Keep only the latest intent; let the in-flight PUT finish, then send coalesced payload.
      this.pendingSave = request;
      this.applySavingFlags(request);
      return;
    }

    this.startSave(request);
  }

  private startSave(request: PendingStructuredSave): void {
    this.applySavingFlags(request);

    this.saveSubscription = this.apiService.saveStructured(toSaveRequest(request.sections)).subscribe({
      next: (document) => {
        this.saveSubscription = null;

        const pending = this.pendingSave;
        this.pendingSave = null;

        if (pending && pending.generation > request.generation) {
          // Stale response relative to a newer local intent — do not apply; send latest.
          this.startSave(pending);
          return;
        }

        if (request.generation !== this.saveGeneration) {
          this.clearSavingFlags();
          return;
        }

        this.clearSavingFlags();
        this.structured.set(this.hydrateForContentEditing(document));
        this.lastSuccessfulSaveGeneration.set(request.generation);
      },
      error: (error) => {
        this.saveSubscription = null;

        const pending = this.pendingSave;
        this.pendingSave = null;

        if (pending && pending.generation > request.generation) {
          // Failed older PUT; still attempt the latest coalesced payload.
          this.startSave(pending);
          return;
        }

        this.clearSavingFlags();

        if (isRequestAborted(error)) {
          return;
        }

        if (request.generation !== this.saveGeneration) {
          return;
        }

        this.saveError.set(
          this.readErrorMessage(
            error,
            request.orderOnly ? 'Could not save section order.' : 'Could not save structured CV content.'
          )
        );
      }
    });
  }

  private applySavingFlags(request: PendingStructuredSave): void {
    if (request.orderOnly) {
      this.savingSectionId.set(null);
      this.savingSectionOrder.set(true);
      return;
    }

    this.savingSectionId.set(request.sectionId);
    this.savingSectionOrder.set(false);
  }

  private clearSavingFlags(): void {
    this.savingSectionId.set(null);
    this.savingSectionOrder.set(false);
  }

  private cancelLoadSubscription(): void {
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = null;
  }

  private cancelAiUpdate(): void {
    this.aiUpdateSubscription?.unsubscribe();
    this.aiUpdateSubscription = null;
  }

  private cancelSuggestions(): void {
    this.suggestionsSubscription?.unsubscribe();
    this.suggestionsSubscription = null;
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
