import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { isRequestAborted } from '../../../core/http/is-request-aborted';
import {
  CvImprovementSuggestion,
  CvStructuredDocument,
  CvStructuredSection
} from '../models/cv-structured.model';
import { toSaveRequest } from '../utils/cv-structured-draft.util';
import { CvDocumentApiService } from './cv-document-api.service';

@Injectable({ providedIn: 'root' })
export class CvStructuredFacade {
  private readonly apiService = inject(CvDocumentApiService);
  private loadSubscription: Subscription | null = null;
  private saveSubscription: Subscription | null = null;
  private aiUpdateSubscription: Subscription | null = null;
  private suggestionsSubscription: Subscription | null = null;

  readonly loading = signal(false);
  readonly savingSectionId = signal<string | null>(null);
  readonly updatingWithAi = signal(false);
  readonly generatingSuggestions = signal(false);
  readonly structured = signal<CvStructuredDocument | null>(null);
  readonly suggestions = signal<CvImprovementSuggestion[]>([]);
  readonly error = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly aiUpdateError = signal<string | null>(null);
  readonly suggestionError = signal<string | null>(null);

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

  save(sections: readonly CvStructuredSection[], sectionId: string): void {
    this.cancelSave();
    this.savingSectionId.set(sectionId);
    this.saveError.set(null);

    this.saveSubscription = this.apiService.saveStructured(toSaveRequest(sections)).subscribe({
      next: (document) => {
        this.savingSectionId.set(null);
        this.structured.set(document);
      },
      error: (error) => {
        this.savingSectionId.set(null);

        if (isRequestAborted(error)) {
          return;
        }

        this.saveError.set(this.readErrorMessage(error, 'Could not save structured CV content.'));
      }
    });
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
          this.structured.set(document);
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
    this.structured.set(document);
  }

  private cancelLoad(): void {
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = null;
  }

  private cancelSave(): void {
    this.saveSubscription?.unsubscribe();
    this.saveSubscription = null;
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
