import { HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Subscription } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { isRequestAborted } from '../../../core/http/is-request-aborted';
import { CvDocument } from '../models/cv-document.model';
import { CvDocumentApiService } from './cv-document-api.service';

@Injectable({ providedIn: 'root' })
export class CvDocumentFacade {
  private readonly authService = inject(AuthService);
  private readonly apiService = inject(CvDocumentApiService);
  private readonly sanitizer = inject(DomSanitizer);
  private loadSubscription: Subscription | null = null;
  private uploadSubscription: Subscription | null = null;
  private deleteSubscription: Subscription | null = null;
  private exportSubscription: Subscription | null = null;
  private previewSubscription: Subscription | null = null;
  private loadedUserId: string | null = null;
  private objectUrl: string | null = null;

  readonly loading = signal(false);
  readonly uploading = signal(false);
  readonly deleting = signal(false);
  readonly exporting = signal(false);
  readonly loadingPreview = signal(false);
  readonly document = signal<CvDocument | null>(null);
  readonly error = signal<string | null>(null);
  readonly uploadError = signal<string | null>(null);
  readonly deleteError = signal<string | null>(null);
  readonly exportError = signal<string | null>(null);
  readonly previewError = signal<string | null>(null);
  readonly blobUrl = signal<string | null>(null);

  readonly previewUrl = computed<SafeResourceUrl | null>(() => {
    const url = this.blobUrl();

    return url ? this.sanitizer.bypassSecurityTrustResourceUrl(url) : null;
  });

  readonly hasDocument = computed(() => this.document() !== null);

  constructor() {
    effect(() => {
      const session = this.authService.session();
      const currentUserId = this.authService.currentUser()?.id ?? null;

      if (!session) {
        this.loadedUserId = null;
        this.resetState();
        return;
      }

      if (!currentUserId) {
        return;
      }

      if (this.loadedUserId !== currentUserId) {
        this.loadedUserId = currentUserId;
        this.resetState();
        this.load();
      }
    });
  }

  load(): void {
    this.cancelLoad();
    this.loading.set(true);
    this.error.set(null);

    this.loadSubscription = this.apiService.getCurrent().subscribe({
      next: (document) => {
        this.loading.set(false);
        this.document.set(document);
        this.loadPreview();
      },
      error: (error) => {
        this.loading.set(false);

        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.document.set(null);
          this.clearPreview();
          return;
        }

        if (isRequestAborted(error)) {
          return;
        }

        this.error.set(this.readErrorMessage(error, 'Could not load your CV.'));
      }
    });
  }

  upload(file: File): void {
    this.cancelUpload();
    this.uploading.set(true);
    this.uploadError.set(null);

    this.uploadSubscription = this.apiService.upload(file).subscribe({
      next: (document) => {
        this.uploading.set(false);
        this.document.set(document);
        this.loadPreview();
      },
      error: (error) => {
        this.uploading.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.uploadError.set(this.readErrorMessage(error, 'Could not upload your CV.'));
      }
    });
  }

  delete(): void {
    this.cancelDelete();
    this.deleting.set(true);
    this.deleteError.set(null);

    this.deleteSubscription = this.apiService.delete().subscribe({
      next: () => {
        this.deleting.set(false);
        this.document.set(null);
        this.clearPreview();
      },
      error: (error) => {
        this.deleting.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.deleteError.set(this.readErrorMessage(error, 'Could not delete your CV.'));
      }
    });
  }

  exportStructured(): void {
    this.cancelExport();
    this.exporting.set(true);
    this.exportError.set(null);

    this.exportSubscription = this.apiService.exportStructured().subscribe({
      next: (document) => {
        this.exporting.set(false);
        this.document.set(document);
        this.loadPreview();
      },
      error: (error) => {
        this.exporting.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.exportError.set(this.readErrorMessage(error, 'Could not export your CV PDF.'));
      }
    });
  }

  private loadPreview(): void {
    this.cancelPreview();
    this.loadingPreview.set(true);
    this.previewError.set(null);

    this.previewSubscription = this.apiService.downloadContent().subscribe({
      next: (blob) => {
        this.loadingPreview.set(false);
        this.setPreviewBlob(blob);
      },
      error: (error) => {
        this.loadingPreview.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.previewError.set(this.readErrorMessage(error, 'Could not load the CV preview.'));
      }
    });
  }

  private setPreviewBlob(blob: Blob): void {
    this.clearObjectUrl();
    this.objectUrl = URL.createObjectURL(blob);
    this.blobUrl.set(this.objectUrl);
  }

  private clearPreview(): void {
    this.cancelPreview();
    this.clearObjectUrl();
    this.blobUrl.set(null);
    this.previewError.set(null);
    this.loadingPreview.set(false);
  }

  private clearObjectUrl(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }

  private resetState(): void {
    this.cancelLoad();
    this.cancelUpload();
    this.cancelDelete();
    this.cancelExport();
    this.clearPreview();
    this.loading.set(false);
    this.uploading.set(false);
    this.deleting.set(false);
    this.exporting.set(false);
    this.document.set(null);
    this.error.set(null);
    this.uploadError.set(null);
    this.deleteError.set(null);
    this.exportError.set(null);
  }

  private cancelLoad(): void {
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = null;
  }

  private cancelUpload(): void {
    this.uploadSubscription?.unsubscribe();
    this.uploadSubscription = null;
  }

  private cancelDelete(): void {
    this.deleteSubscription?.unsubscribe();
    this.deleteSubscription = null;
  }

  private cancelExport(): void {
    this.exportSubscription?.unsubscribe();
    this.exportSubscription = null;
  }

  private cancelPreview(): void {
    this.previewSubscription?.unsubscribe();
    this.previewSubscription = null;
  }

  private readErrorMessage(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const payload = (error as { error: unknown }).error;

      if (typeof payload === 'string' && payload.trim()) {
        return payload;
      }

      if (
        typeof payload === 'object' &&
        payload !== null &&
        'title' in payload &&
        typeof (payload as { title: unknown }).title === 'string'
      ) {
        return (payload as { title: string }).title;
      }
    }

    return fallback;
  }
}
