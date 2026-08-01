import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { isRequestAborted } from '../../../core/http/is-request-aborted';
import {
  CvImprovementSuggestion,
  CvQualityEvaluation,
  CvStructuredDocument,
  CvStructuredSection
} from '../models/cv-structured.model';
import {
  assistMergeRequiresPersist,
  mergeAssistStructuredUpdate
} from '../utils/cv-structured-assist-merge.util';
import { toSaveRequest, hydrateStructuredDocument } from '../utils/cv-structured-draft.util';
import { normalizeSectionsForEditing } from '../utils/cv-structured-edit-normalizer.util';
import { CvDocumentApiService } from './cv-document-api.service';

type PendingStructuredSave = {
  sections: readonly CvStructuredSection[];
  sectionId: string | null;
  generation: number;
};

@Injectable({ providedIn: 'root' })
export class CvStructuredFacade {
  private readonly apiService = inject(CvDocumentApiService);
  private loadSubscription: Subscription | null = null;
  private saveSubscription: Subscription | null = null;
  private aiUpdateSubscription: Subscription | null = null;
  private suggestionsSubscription: Subscription | null = null;
  private evaluationSubscription: Subscription | null = null;

  private loadGeneration = 0;
  private saveGeneration = 0;
  private pendingSave: PendingStructuredSave | null = null;

  readonly loading = signal(false);
  readonly savingSectionId = signal<string | null>(null);
  readonly updatingWithAi = signal(false);
  readonly generatingSuggestions = signal(false);
  readonly evaluating = signal(false);
  readonly structured = signal<CvStructuredDocument | null>(null);
  readonly suggestions = signal<CvImprovementSuggestion[]>([]);
  /** Session-only evaluation report — never written to storage (D2). */
  readonly evaluation = signal<CvQualityEvaluation | null>(null);
  readonly error = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly aiUpdateError = signal<string | null>(null);
  readonly suggestionError = signal<string | null>(null);
  readonly evaluationError = signal<string | null>(null);
  /** Monotonic generation of the last successfully applied structured save. */
  readonly lastSuccessfulSaveGeneration = signal(0);

  readonly isSaving = computed(() => this.savingSectionId() !== null);

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
        this.setStructured(document);
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

    // Merge against live local state at response time (user edits during wait stay).
    this.aiUpdateSubscription = this.apiService
      .updateStructuredWithAi(trimmedInstructions, sectionIds)
      .subscribe({
        next: (document) => {
          this.updatingWithAi.set(false);

          // API persists the model body as a full replace; models often return a
          // partial/focus-only document. Merge by section id so non-targeted
          // sections (and Contact) survive, then corrective-save when needed.
          const merged = mergeAssistStructuredUpdate(this.structured(), document, sectionIds);
          this.setStructured(merged);

          const applied = this.structured();
          const aiNormalized = this.hydrateForContentEditing(document);
          if (applied && assistMergeRequiresPersist(aiNormalized, applied)) {
            const persistSectionId = sectionIds?.[0] ?? applied.sections[0]?.id ?? 'assist-merge';
            this.save(applied.sections, persistSectionId);
          }
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

  /**
   * Run ephemeral CV quality evaluation. Does not mutate Structured CV.
   * Result stays in memory only — never localStorage / IndexedDB / backend save (D2).
   */
  evaluateQuality(maxFindings = 8): void {
    if (this.evaluating()) {
      return;
    }

    this.cancelEvaluation();
    this.evaluating.set(true);
    this.evaluationError.set(null);

    this.evaluationSubscription = this.apiService.evaluateStructuredQuality(maxFindings).subscribe({
      next: (result) => {
        this.evaluating.set(false);
        this.evaluation.set(result);
      },
      error: (error) => {
        this.evaluating.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.evaluationError.set(
          this.readErrorMessage(error, 'Could not evaluate CV quality.')
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

  clearEvaluationError(): void {
    this.evaluationError.set(null);
  }

  clearEvaluation(): void {
    this.cancelEvaluation();
    this.evaluation.set(null);
    this.evaluationError.set(null);
    this.evaluating.set(false);
  }

  /**
   * Single Structured CV edit ingress: pass the raw API DTO (do not pre-hydrate).
   * Applies `hydrateForContentEditing` (hydrate + ADR-0003 edit normalize).
   */
  setStructured(document: CvStructuredDocument): void {
    this.structured.set(this.hydrateForContentEditing(document));
  }

  /**
   * Hydrate API payload and normalize edit slots (Summary/Skills/Contact),
   * including Contact modern expand + absorb/dedupe so Minimal/Modern canvases
   * never show unlabeled orphans beside empty Email/Phone/LinkedIn starters.
   * Sole normalize path for edit (ADR-0003); keep idempotent — see util specs.
   */
  hydrateForContentEditing(document: CvStructuredDocument): CvStructuredDocument {
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
      this.savingSectionId.set(request.sectionId);
      return;
    }

    this.startSave(request);
  }

  private startSave(request: PendingStructuredSave): void {
    this.savingSectionId.set(request.sectionId);

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
          this.savingSectionId.set(null);
          return;
        }

        this.savingSectionId.set(null);
        this.setStructured(document);
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

        this.savingSectionId.set(null);

        if (isRequestAborted(error)) {
          return;
        }

        if (request.generation !== this.saveGeneration) {
          return;
        }

        this.saveError.set(this.readErrorMessage(error, 'Could not save structured CV content.'));
      }
    });
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

  private cancelEvaluation(): void {
    this.evaluationSubscription?.unsubscribe();
    this.evaluationSubscription = null;
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
