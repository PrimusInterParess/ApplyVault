import { HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Subscription } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { isRequestAborted } from '../../../core/http/is-request-aborted';
import { CvDocument, CvStructuredImportSummary } from '../models/cv-document.model';
import {
  CV_EXPORT_MAX_PAGES_STORAGE_KEY,
  CV_EXPORT_TEMPLATE_STORAGE_KEY,
  DEFAULT_CV_EXPORT_MAX_PAGES,
  DEFAULT_CV_EXPORT_TEMPLATE_ID,
  MAX_CV_EXPORT_TEMPLATE_ID
} from '../models/cv-export-template.model';
import { CvDocumentApiService } from './cv-document-api.service';
import { CvStructuredFacade } from './cv-structured.facade';

@Injectable({ providedIn: 'root' })
export class CvDocumentFacade {
  private readonly authService = inject(AuthService);
  private readonly apiService = inject(CvDocumentApiService);
  private readonly cvStructured = inject(CvStructuredFacade);
  private readonly sanitizer = inject(DomSanitizer);
  private loadSubscription: Subscription | null = null;
  private uploadSubscription: Subscription | null = null;
  private reimportSubscription: Subscription | null = null;
  private deleteSubscription: Subscription | null = null;
  private downloadOriginalSubscription: Subscription | null = null;
  private downloadFormattedSubscription: Subscription | null = null;
  private profilePhotoSubscription: Subscription | null = null;
  private loadedUserId: string | null = null;
  private profilePhotoObjectUrl: string | null = null;
  private previewObjectUrl: string | null = null;
  private previewBlob: Blob | null = null;

  readonly loading = signal(false);
  readonly uploading = signal(false);
  readonly reimporting = signal(false);
  readonly deleting = signal(false);
  readonly downloadingOriginal = signal(false);
  readonly downloadingFormatted = signal(false);
  readonly previewLoading = signal(false);
  readonly loadingProfilePhoto = signal(false);
  readonly document = signal<CvDocument | null>(null);
  readonly importSummary = signal<CvStructuredImportSummary | null>(null);
  readonly error = signal<string | null>(null);
  readonly uploadError = signal<string | null>(null);
  readonly reimportError = signal<string | null>(null);
  readonly deleteError = signal<string | null>(null);
  readonly downloadOriginalError = signal<string | null>(null);
  readonly downloadFormattedError = signal<string | null>(null);
  readonly previewError = signal<string | null>(null);
  readonly profilePhotoError = signal<string | null>(null);
  readonly profilePhotoUrl = signal<string | null>(null);
  readonly selectedExportTemplateId = signal(this.readStoredExportTemplateId());
  readonly selectedExportMaxPages = signal<number | null>(this.readStoredExportMaxPages());
  readonly previewOpen = signal(false);
  readonly previewPageCount = signal<number | null>(null);
  readonly previewMaxPages = signal<number | null>(null);
  readonly previewExceedsLimit = signal(false);
  readonly previewNotice = signal<string | null>(null);
  readonly previewBlobUrl = signal<SafeResourceUrl | null>(null);

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

    effect(() => {
      this.cvStructured.structured();
      this.clearFormattedPreview();
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
    this.clearFormattedPreview();
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
        this.clearFormattedPreview();

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
        this.clearFormattedPreview();
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

  setExportTemplateId(templateId: number): void {
    this.selectedExportTemplateId.set(templateId);
    this.clearFormattedPreview();

    try {
      sessionStorage.setItem(CV_EXPORT_TEMPLATE_STORAGE_KEY, String(templateId));
    } catch {
      // Ignore storage failures (private mode, quota, etc.).
    }
  }

  setExportMaxPages(maxPages: number | null): void {
    this.selectedExportMaxPages.set(maxPages);
    this.clearFormattedPreview();

    try {
      if (maxPages === null) {
        sessionStorage.removeItem(CV_EXPORT_MAX_PAGES_STORAGE_KEY);
      } else {
        sessionStorage.setItem(CV_EXPORT_MAX_PAGES_STORAGE_KEY, String(maxPages));
      }
    } catch {
      // Ignore storage failures (private mode, quota, etc.).
    }
  }

  downloadFormatted(): void {
    this.previewFormatted();
  }

  previewFormatted(): void {
    const document = this.document();

    if (!document?.hasStructuredContent) {
      return;
    }

    const templateId = this.selectedExportTemplateId();
    const maxPages = this.selectedExportMaxPages();

    this.cancelDownloadFormatted();
    this.clearFormattedPreviewBlob();
    this.previewOpen.set(true);
    this.previewLoading.set(true);
    this.downloadingFormatted.set(true);
    this.downloadFormattedError.set(null);
    this.previewError.set(null);
    this.previewPageCount.set(null);
    this.previewMaxPages.set(maxPages);
    this.previewExceedsLimit.set(false);
    this.previewNotice.set(null);

    this.downloadFormattedSubscription = this.apiService
      .downloadFormattedPdf({ templateId, maxPages })
      .subscribe({
        next: (result) => {
          this.previewLoading.set(false);
          this.downloadingFormatted.set(false);
          this.previewPageCount.set(result.pageCount);
          this.previewMaxPages.set(result.maxPages);
          this.previewExceedsLimit.set(result.exceedsLimit);
          this.previewNotice.set(result.notice);
          this.setFormattedPreviewBlob(result.blob);
        },
        error: (error) => {
          this.previewLoading.set(false);
          this.downloadingFormatted.set(false);

          if (isRequestAborted(error)) {
            return;
          }

          const message = this.readErrorMessage(error, 'Could not preview your formatted CV PDF.');
          this.previewError.set(message);
          this.downloadFormattedError.set(message);
        }
      });
  }

  downloadFormattedFromPreview(): void {
    const document = this.document();

    if (!document || !this.previewBlob) {
      return;
    }

    const baseName = document.originalFileName.replace(/\.pdf$/i, '');
    this.triggerDownload(this.previewBlob, `${baseName}-export.pdf`);
  }

  closePreview(): void {
    this.clearFormattedPreview();
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

  private setFormattedPreviewBlob(blob: Blob): void {
    this.clearFormattedPreviewBlob();
    this.previewBlob = blob;
    this.previewObjectUrl = URL.createObjectURL(blob);
    this.previewBlobUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.previewObjectUrl));
  }

  private clearFormattedPreview(): void {
    this.cancelDownloadFormatted();
    this.previewOpen.set(false);
    this.previewLoading.set(false);
    this.previewError.set(null);
    this.previewPageCount.set(null);
    this.previewMaxPages.set(null);
    this.previewExceedsLimit.set(false);
    this.previewNotice.set(null);
    this.clearFormattedPreviewBlob();
  }

  private clearFormattedPreviewBlob(): void {
    if (this.previewObjectUrl) {
      URL.revokeObjectURL(this.previewObjectUrl);
      this.previewObjectUrl = null;
    }

    this.previewBlob = null;
    this.previewBlobUrl.set(null);
  }

  private resetState(): void {
    this.cancelLoad();
    this.cancelUpload();
    this.cancelReimport();
    this.cancelDelete();
    this.cancelDownloadOriginal();
    this.cancelDownloadFormatted();
    this.clearFormattedPreview();
    this.clearProfilePhoto();
    this.loading.set(false);
    this.uploading.set(false);
    this.reimporting.set(false);
    this.deleting.set(false);
    this.downloadingOriginal.set(false);
    this.downloadingFormatted.set(false);
    this.previewLoading.set(false);
    this.document.set(null);
    this.importSummary.set(null);
    this.error.set(null);
    this.uploadError.set(null);
    this.reimportError.set(null);
    this.deleteError.set(null);
    this.downloadOriginalError.set(null);
    this.downloadFormattedError.set(null);
    this.previewError.set(null);
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

  private readStoredExportTemplateId(): number {
    try {
      const stored = sessionStorage.getItem(CV_EXPORT_TEMPLATE_STORAGE_KEY);

      if (!stored) {
        return DEFAULT_CV_EXPORT_TEMPLATE_ID;
      }

      const parsed = Number.parseInt(stored, 10);

      return Number.isInteger(parsed) && parsed >= 1 && parsed <= MAX_CV_EXPORT_TEMPLATE_ID
        ? parsed
        : DEFAULT_CV_EXPORT_TEMPLATE_ID;
    } catch {
      return DEFAULT_CV_EXPORT_TEMPLATE_ID;
    }
  }

  private readStoredExportMaxPages(): number | null {
    try {
      const stored = sessionStorage.getItem(CV_EXPORT_MAX_PAGES_STORAGE_KEY);

      if (!stored) {
        return DEFAULT_CV_EXPORT_MAX_PAGES;
      }

      const parsed = Number.parseInt(stored, 10);

      return Number.isInteger(parsed) && parsed >= 1 && parsed <= 2
        ? parsed
        : DEFAULT_CV_EXPORT_MAX_PAGES;
    } catch {
      return DEFAULT_CV_EXPORT_MAX_PAGES;
    }
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
