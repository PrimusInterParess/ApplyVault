import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { isRequestAborted } from '../../../core/http/is-request-aborted';
import { CvStructuredDocument } from '../models/cv-structured.model';
import { CvDocumentApiService } from './cv-document-api.service';

@Injectable({ providedIn: 'root' })
export class CvStructuredFacade {
  private readonly apiService = inject(CvDocumentApiService);
  private loadSubscription: Subscription | null = null;

  readonly loading = signal(false);
  readonly structured = signal<CvStructuredDocument | null>(null);
  readonly error = signal<string | null>(null);

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

  private cancelLoad(): void {
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = null;
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
