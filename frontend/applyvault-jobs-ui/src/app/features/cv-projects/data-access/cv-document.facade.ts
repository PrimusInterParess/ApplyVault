import { HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Subscription } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { isRequestAborted } from '../../../core/http/is-request-aborted';
import { CvDocument } from '../models/cv-document.model';
import { SaveCvStructuredDocumentRequest } from '../models/cv-structured.model';
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
  private downloadOriginalSubscription: Subscription | null = null;
  private previewSubscription: Subscription | null = null;
  private draftPreviewSubscription: Subscription | null = null;
  private loadedUserId: string | null = null;
  private objectUrl: string | null = null;
  private draftObjectUrl: string | null = null;
  private draftPreviewRequestKey: string | null = null;
  private draftPreviewInFlightKey: string | null = null;

  readonly loading = signal(false);
  readonly uploading = signal(false);
  readonly deleting = signal(false);
  readonly exporting = signal(false);
  readonly downloadingOriginal = signal(false);
  readonly loadingPreview = signal(false);
  readonly document = signal<CvDocument | null>(null);
  readonly error = signal<string | null>(null);
  readonly uploadError = signal<string | null>(null);
  readonly deleteError = signal<string | null>(null);
  readonly exportError = signal<string | null>(null);
  readonly downloadOriginalError = signal<string | null>(null);
  readonly previewError = signal<string | null>(null);
  readonly blobUrl = signal<string | null>(null);
  readonly loadingDraftPreview = signal(false);
  readonly refreshingDraftPreview = signal(false);
  readonly draftPreviewError = signal<string | null>(null);
  readonly draftBlobUrl = signal<string | null>(null);

  readonly previewUrl = computed<SafeResourceUrl | null>(() => {
    const url = this.blobUrl();

    return url ? this.sanitizer.bypassSecurityTrustResourceUrl(url) : null;
  });

  readonly draftPreviewUrl = computed<SafeResourceUrl | null>(() => {
    const url = this.draftBlobUrl();

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

  refreshDraftPreview(request: SaveCvStructuredDocumentRequest, force = false): void {
    if (request.sections.length === 0) {
      this.clearDraftPreview();
      return;
    }

    const requestKey = JSON.stringify(request);

    if (!force && requestKey === this.draftPreviewRequestKey && this.draftBlobUrl()) {
      return;
    }

    if (!force && requestKey === this.draftPreviewInFlightKey) {
      return;
    }

    this.cancelDraftPreview();

    const hasPreview = this.draftBlobUrl() !== null;
    this.loadingDraftPreview.set(!hasPreview);
    this.refreshingDraftPreview.set(hasPreview);
    this.draftPreviewError.set(null);
    this.draftPreviewInFlightKey = requestKey;

    this.draftPreviewSubscription = this.apiService.previewStructured(request).subscribe({
      next: (blob) => {
        this.draftPreviewRequestKey = requestKey;
        this.draftPreviewInFlightKey = null;
        this.loadingDraftPreview.set(false);
        this.refreshingDraftPreview.set(false);
        this.setDraftPreviewBlob(blob);
      },
      error: (error) => {
        if (isRequestAborted(error)) {
          return;
        }

        this.draftPreviewInFlightKey = null;
        this.loadingDraftPreview.set(false);
        this.refreshingDraftPreview.set(false);

        void this.resolveErrorMessage(error, 'Could not load the draft CV preview.').then((message) => {
          this.draftPreviewError.set(message);
        });
      }
    });
  }

  clearDraftPreview(): void {
    this.cancelDraftPreview();
    this.clearDraftObjectUrl();
    this.draftBlobUrl.set(null);
    this.draftPreviewError.set(null);
    this.draftPreviewRequestKey = null;
    this.draftPreviewInFlightKey = null;
    this.loadingDraftPreview.set(false);
    this.refreshingDraftPreview.set(false);
  }

  exportStructured(): void {
    this.cancelExport();
    this.exporting.set(true);
    this.exportError.set(null);

    this.exportSubscription = this.apiService.exportStructured().subscribe({
      next: (document) => {
        this.exporting.set(false);
        this.document.set(document);
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

  downloadOriginal(): void {
    const document = this.document();

    if (!document) {
      return;
    }

    this.cancelDownloadOriginal();
    this.downloadingOriginal.set(true);
    this.downloadOriginalError.set(null);

    this.downloadOriginalSubscription = this.apiService.downloadOriginalContent().subscribe({
      next: (blob) => {
        this.downloadingOriginal.set(false);
        this.triggerDownload(blob, document.originalFileName);
      },
      error: (error) => {
        this.downloadingOriginal.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.downloadOriginalError.set(
          this.readErrorMessage(error, 'Could not download your original CV PDF.')
        );
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

  private setDraftPreviewBlob(blob: Blob): void {
    const previousUrl = this.draftObjectUrl;
    this.draftObjectUrl = URL.createObjectURL(blob);
    this.draftBlobUrl.set(this.draftObjectUrl);

    if (previousUrl) {
      queueMicrotask(() => URL.revokeObjectURL(previousUrl));
    }
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

  private clearDraftObjectUrl(): void {
    if (this.draftObjectUrl) {
      URL.revokeObjectURL(this.draftObjectUrl);
      this.draftObjectUrl = null;
    }
  }

  private resetState(): void {
    this.cancelLoad();
    this.cancelUpload();
    this.cancelDelete();
    this.cancelExport();
    this.cancelDownloadOriginal();
    this.clearPreview();
    this.clearDraftPreview();
    this.loading.set(false);
    this.uploading.set(false);
    this.deleting.set(false);
    this.exporting.set(false);
    this.downloadingOriginal.set(false);
    this.document.set(null);
    this.error.set(null);
    this.uploadError.set(null);
    this.deleteError.set(null);
    this.exportError.set(null);
    this.downloadOriginalError.set(null);
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

  private cancelDownloadOriginal(): void {
    this.downloadOriginalSubscription?.unsubscribe();
    this.downloadOriginalSubscription = null;
  }

  private triggerDownload(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  private cancelPreview(): void {
    this.previewSubscription?.unsubscribe();
    this.previewSubscription = null;
  }

  private cancelDraftPreview(): void {
    this.draftPreviewSubscription?.unsubscribe();
    this.draftPreviewSubscription = null;
  }

  private async resolveErrorMessage(error: unknown, fallback: string): Promise<string> {
    if (error instanceof HttpErrorResponse) {
      if (error.error instanceof Blob) {
        const text = await error.error.text();

        if (text.trim()) {
          return text;
        }
      }
    }

    return this.readErrorMessage(error, fallback);
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
