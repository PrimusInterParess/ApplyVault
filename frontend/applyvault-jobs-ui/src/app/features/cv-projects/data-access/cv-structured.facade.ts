import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { isRequestAborted } from '../../../core/http/is-request-aborted';
import { CvStructuredDocument, CvStructuredSection } from '../models/cv-structured.model';
import { toSaveRequest } from '../utils/cv-structured-draft.util';
import { CvDocumentApiService } from './cv-document-api.service';

@Injectable({ providedIn: 'root' })
export class CvStructuredFacade {
  private readonly apiService = inject(CvDocumentApiService);
  private loadSubscription: Subscription | null = null;
  private saveSubscription: Subscription | null = null;

  readonly loading = signal(false);
  readonly savingSectionId = signal<string | null>(null);
  readonly structured = signal<CvStructuredDocument | null>(null);
  readonly error = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);

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

  clearSaveError(): void {
    this.saveError.set(null);
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
