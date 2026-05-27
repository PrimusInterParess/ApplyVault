import { HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { isRequestAborted } from '../../../core/http/is-request-aborted';
import { CvDocument, CvStructuredImportSummary } from '../models/cv-document.model';
import { CvDocumentApiService } from './cv-document-api.service';
import { CvStructuredFacade } from './cv-structured.facade';

@Injectable({ providedIn: 'root' })
export class CvDocumentFacade {
  private readonly authService = inject(AuthService);
  private readonly apiService = inject(CvDocumentApiService);
  private readonly cvStructured = inject(CvStructuredFacade);
  private loadSubscription: Subscription | null = null;
  private uploadSubscription: Subscription | null = null;
  private reimportSubscription: Subscription | null = null;
  private deleteSubscription: Subscription | null = null;
  private downloadOriginalSubscription: Subscription | null = null;
  private downloadFormattedSubscription: Subscription | null = null;
  private profilePhotoSubscription: Subscription | null = null;
  private loadedUserId: string | null = null;
  private profilePhotoObjectUrl: string | null = null;

  readonly loading = signal(false);
  readonly uploading = signal(false);
  readonly reimporting = signal(false);
  readonly deleting = signal(false);
  readonly downloadingOriginal = signal(false);
  readonly downloadingFormatted = signal(false);
  readonly loadingProfilePhoto = signal(false);
  readonly document = signal<CvDocument | null>(null);
  readonly importSummary = signal<CvStructuredImportSummary | null>(null);
  readonly error = signal<string | null>(null);
  readonly uploadError = signal<string | null>(null);
  readonly reimportError = signal<string | null>(null);
  readonly deleteError = signal<string | null>(null);
  readonly downloadOriginalError = signal<string | null>(null);
  readonly downloadFormattedError = signal<string | null>(null);
  readonly profilePhotoError = signal<string | null>(null);
  readonly profilePhotoUrl = signal<string | null>(null);

  readonly hasDocument = computed(() => this.document() !== null);

  readonly extracting = computed(() => this.uploading() || this.reimporting());

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
        this.loadProfilePhoto(document);
      },
      error: (error) => {
        this.loading.set(false);

        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.document.set(null);
          this.clearProfilePhoto();
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
    this.importSummary.set(null);
    this.clearProfilePhoto();

    this.uploadSubscription = this.apiService.upload(file).subscribe({
      next: (result) => {
        this.uploading.set(false);
        this.document.set(result.document);
        this.importSummary.set(result.import);
        this.loadProfilePhoto(result.document);
        this.cvStructured.load();
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

  reimportStructured(): void {
    if (!this.document()) {
      return;
    }

    this.cancelReimport();
    this.reimporting.set(true);
    this.reimportError.set(null);

    this.reimportSubscription = this.apiService.reimportStructured().subscribe({
      next: (result) => {
        this.reimporting.set(false);
        this.importSummary.set(result.import);

        if (result.structured) {
          this.cvStructured.setStructured(result.structured);
        } else {
          this.cvStructured.load();
        }
      },
      error: (error) => {
        this.reimporting.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.reimportError.set(this.readErrorMessage(error, 'Could not re-import CV sections.'));
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
        this.importSummary.set(null);
        this.clearProfilePhoto();
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

  downloadFormatted(): void {
    const document = this.document();

    if (!document?.hasStructuredContent) {
      return;
    }

    this.cancelDownloadFormatted();
    this.downloadingFormatted.set(true);
    this.downloadFormattedError.set(null);

    this.downloadFormattedSubscription = this.apiService.downloadFormattedPdf().subscribe({
      next: (blob) => {
        this.downloadingFormatted.set(false);
        const baseName = document.originalFileName.replace(/\.pdf$/i, '');
        this.triggerDownload(blob, `${baseName}-export.pdf`);
      },
      error: (error) => {
        this.downloadingFormatted.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.downloadFormattedError.set(
          this.readErrorMessage(error, 'Could not download your formatted CV PDF.')
        );
      }
    });
  }

  private loadProfilePhoto(document: CvDocument): void {
    this.cancelProfilePhoto();

    if (!document.hasProfilePhoto) {
      this.clearProfilePhoto();
      return;
    }

    this.loadingProfilePhoto.set(true);
    this.profilePhotoError.set(null);

    this.profilePhotoSubscription = this.apiService.downloadProfilePhoto().subscribe({
      next: (blob) => {
        this.loadingProfilePhoto.set(false);
        this.setProfilePhotoBlob(blob);
      },
      error: (error) => {
        this.loadingProfilePhoto.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.profilePhotoError.set(this.readErrorMessage(error, 'Could not load your profile photo.'));
      }
    });
  }

  private setProfilePhotoBlob(blob: Blob): void {
    this.clearProfilePhotoObjectUrl();
    this.profilePhotoObjectUrl = URL.createObjectURL(blob);
    this.profilePhotoUrl.set(this.profilePhotoObjectUrl);
  }

  private clearProfilePhoto(): void {
    this.cancelProfilePhoto();
    this.clearProfilePhotoObjectUrl();
    this.profilePhotoUrl.set(null);
    this.profilePhotoError.set(null);
    this.loadingProfilePhoto.set(false);
  }

  private clearProfilePhotoObjectUrl(): void {
    if (this.profilePhotoObjectUrl) {
      URL.revokeObjectURL(this.profilePhotoObjectUrl);
      this.profilePhotoObjectUrl = null;
    }
  }

  private resetState(): void {
    this.cancelLoad();
    this.cancelUpload();
    this.cancelReimport();
    this.cancelDelete();
    this.cancelDownloadOriginal();
    this.cancelDownloadFormatted();
    this.clearProfilePhoto();
    this.loading.set(false);
    this.uploading.set(false);
    this.reimporting.set(false);
    this.deleting.set(false);
    this.downloadingOriginal.set(false);
    this.downloadingFormatted.set(false);
    this.document.set(null);
    this.importSummary.set(null);
    this.error.set(null);
    this.uploadError.set(null);
    this.reimportError.set(null);
    this.deleteError.set(null);
    this.downloadOriginalError.set(null);
    this.downloadFormattedError.set(null);
  }

  private cancelLoad(): void {
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = null;
  }

  private cancelUpload(): void {
    this.uploadSubscription?.unsubscribe();
    this.uploadSubscription = null;
  }

  private cancelReimport(): void {
    this.reimportSubscription?.unsubscribe();
    this.reimportSubscription = null;
  }

  private cancelDelete(): void {
    this.deleteSubscription?.unsubscribe();
    this.deleteSubscription = null;
  }

  private cancelDownloadOriginal(): void {
    this.downloadOriginalSubscription?.unsubscribe();
    this.downloadOriginalSubscription = null;
  }

  private cancelDownloadFormatted(): void {
    this.downloadFormattedSubscription?.unsubscribe();
    this.downloadFormattedSubscription = null;
  }

  private cancelProfilePhoto(): void {
    this.profilePhotoSubscription?.unsubscribe();
    this.profilePhotoSubscription = null;
  }

  private triggerDownload(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
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
