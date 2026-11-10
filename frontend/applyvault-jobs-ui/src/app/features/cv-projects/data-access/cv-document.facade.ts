import { HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { of, Subscription } from 'rxjs';
import { switchMap } from 'rxjs/operators';

import { AuthService } from '../../../core/auth/auth.service';
import { isRequestAborted } from '../../../core/http/is-request-aborted';
import { CvDocument, CvStructuredImportSummary } from '../models/cv-document.model';
import { CvDocumentApiService } from './cv-document-api.service';
import { CvStructuredFacade } from './cv-structured.facade';
import {
  buildCvExportDownloadFileName,
  resolveCvExportPersonName
} from '../utils/cv-export-download-file-name.util';

@Injectable({ providedIn: 'root' })
export class CvDocumentFacade {
  private readonly authService = inject(AuthService);
  private readonly apiService = inject(CvDocumentApiService);
  private readonly cvStructured = inject(CvStructuredFacade);
  private readonly sanitizer = inject(DomSanitizer);
  private loadSubscription: Subscription | null = null;
  private uploadSubscription: Subscription | null = null;
  private startBlankSubscription: Subscription | null = null;
  private downloadFormattedFileSubscription: Subscription | null = null;
  private exportHtmlPreviewSubscription: Subscription | null = null;
  private exportHtmlPreviewGeneration = 0;
  private profilePhotoSubscription: Subscription | null = null;
  private profilePhotoUploadSubscription: Subscription | null = null;
  private profilePhotoDeleteSubscription: Subscription | null = null;
  private unplacedSubscription: Subscription | null = null;
  private deleteSubscription: Subscription | null = null;
  private loadedUserId: string | null = null;
  private profilePhotoObjectUrl: string | null = null;

  readonly loading = signal(false);
  readonly uploading = signal(false);
  readonly startingBlank = signal(false);
  readonly downloadingFormatted = signal(false);
  readonly exportHtmlPreviewLoading = signal(false);
  readonly loadingProfilePhoto = signal(false);
  readonly uploadingProfilePhoto = signal(false);
  readonly deletingProfilePhoto = signal(false);
  readonly document = signal<CvDocument | null>(null);
  readonly importSummary = signal<CvStructuredImportSummary | null>(null);
  readonly error = signal<string | null>(null);
  readonly uploadError = signal<string | null>(null);
  readonly startBlankError = signal<string | null>(null);
  readonly downloadFormattedError = signal<string | null>(null);
  readonly exportHtmlPreviewError = signal<string | null>(null);
  readonly profilePhotoError = signal<string | null>(null);
  readonly unplacedBusy = signal(false);
  readonly unplacedError = signal<string | null>(null);
  readonly deleting = signal(false);
  readonly deleteError = signal<string | null>(null);
  readonly profilePhotoUrl = signal<string | null>(null);
  /** Sandboxed iframe srcdoc for Check-export fidelity preview. */
  readonly exportHtmlPreviewSrcdoc = signal<SafeHtml | null>(null);
  /** Additive preview notice from X-Cv-Export-Notice (canvas). */
  readonly exportHtmlPreviewNotice = signal<string | null>(null);
  /** Additive compact level from X-Cv-Export-Compact-Level (canvas). */
  readonly exportHtmlPreviewCompactLevel = signal<number | null>(null);

  readonly hasDocument = computed(() => this.document() !== null);
  readonly unplacedContent = computed(() => this.document()?.unplacedContent ?? []);
  readonly unplacedCount = computed(() => this.unplacedContent().length);

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
        this.document.set(withDocumentDefaults(document));
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

  upload(file: File, onComplete?: () => void): void {
    this.cancelUpload();
    this.uploading.set(true);
    this.uploadError.set(null);
    this.importSummary.set(null);
    this.clearProfilePhoto();

    this.uploadSubscription = this.apiService
      .upload(file)
      .pipe(
        switchMap((result) => {
          this.document.set(withDocumentDefaults(result.document));
          this.importSummary.set(withImportDefaults(result.import));
          this.loadProfilePhoto(result.document);

          if (!result.import.succeeded) {
            this.uploadError.set(
              result.import.notice ?? 'Could not extract text from this PDF.'
            );
            this.cvStructured.clearStructured();
            return of(null);
          }

          return this.apiService.getStructured();
        })
      )
      .subscribe({
        next: (structured) => {
          this.uploading.set(false);
          if (structured) {
            // Raw DTO — setStructured / hydrateForContentEditing is the sole hydrate+normalize ingress.
            this.cvStructured.setStructured(structured);
          }
          onComplete?.();
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

  /** Blank CV is seeded server-side; the builder reloads structured after document identity changes. */
  startBlank(onComplete?: () => void): void {
    this.cancelStartBlank();
    this.startingBlank.set(true);
    this.startBlankError.set(null);
    this.importSummary.set(null);
    this.clearProfilePhoto();

    this.startBlankSubscription = this.apiService.startBlank().subscribe({
      next: (document) => {
        this.startingBlank.set(false);
        this.document.set(withDocumentDefaults(document));
        onComplete?.();
      },
      error: (error) => {
        this.startingBlank.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.startBlankError.set(this.readErrorMessage(error, 'Could not start a new CV.'));
      }
    });
  }

  dismissUnplaced(index: number): void {
    if (index < 0 || index >= this.unplacedContent().length) {
      return;
    }

    this.replaceUnplacedContent(this.unplacedContent().filter((_, itemIndex) => itemIndex !== index));
  }

  dismissAllUnplaced(): void {
    if (this.unplacedContent().length === 0) {
      return;
    }

    this.replaceUnplacedContent([]);
  }

  deleteCurrent(): void {
    if (this.deleting() || !this.document()) {
      return;
    }

    this.cancelDelete();
    this.deleting.set(true);
    this.deleteError.set(null);

    this.deleteSubscription = this.apiService.delete().subscribe({
      next: () => {
        this.deleting.set(false);
        this.cvStructured.clearStructured();
        this.resetState();
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

  replaceUnplacedContent(items: readonly string[]): void {
    if (!this.document()) {
      return;
    }

    this.cancelUnplaced();
    this.unplacedBusy.set(true);
    this.unplacedError.set(null);

    this.unplacedSubscription = this.apiService.replaceUnplacedContent({ items: [...items] }).subscribe({
      next: (document) => {
        this.unplacedBusy.set(false);
        this.document.set(withDocumentDefaults(document));
      },
      error: (error) => {
        this.unplacedBusy.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.unplacedError.set(this.readErrorMessage(error, 'Could not update unplaced content.'));
      }
    });
  }

  /**
   * Load server HTML for the sandboxed fidelity iframe.
   * Same export pipeline as PDF download so BE compact CSS matches export.
   */
  refreshExportHtmlPreview(): void {
    if (!this.canExportFormatted()) {
      this.clearExportHtmlPreview();
      this.exportHtmlPreviewError.set(
        this.document()
          ? 'Save your CV sections before loading the export preview.'
          : 'Create or upload a CV before loading the export preview.'
      );
      return;
    }

    const previewGeneration = ++this.exportHtmlPreviewGeneration;
    this.cancelExportHtmlPreview();
    this.exportHtmlPreviewLoading.set(true);
    this.exportHtmlPreviewError.set(null);
    // Keep last srcdoc/notice/compact until the next successful response (no blank canvas).

    this.exportHtmlPreviewSubscription = this.apiService
      .getExportPreviewHtml()
      .subscribe({
        next: (preview) => {
          if (previewGeneration !== this.exportHtmlPreviewGeneration) {
            return;
          }

          this.exportHtmlPreviewLoading.set(false);
          this.exportHtmlPreviewSrcdoc.set(this.sanitizer.bypassSecurityTrustHtml(preview.html));
          this.exportHtmlPreviewNotice.set(preview.notice);
          this.exportHtmlPreviewCompactLevel.set(preview.compactLevel);
        },
        error: (error) => {
          if (previewGeneration !== this.exportHtmlPreviewGeneration) {
            return;
          }

          this.exportHtmlPreviewLoading.set(false);

          if (isRequestAborted(error)) {
            return;
          }

          // Keep last HTML on failure; surface error for status UI.
          if (error instanceof HttpErrorResponse && error.status === 404) {
            this.exportHtmlPreviewError.set(
              'Export preview is not available yet. Structure editing still works; PDF download uses the export endpoint.'
            );
            return;
          }

          this.exportHtmlPreviewError.set(
            this.readErrorMessage(error, 'Could not load the export HTML preview.')
          );
        }
      });
  }

  /** Fetch and save the formatted PDF to disk (does not require a preview modal). */
  downloadFormattedFile(): void {
    const document = this.document();

    if (!this.canExportFormatted()) {
      this.downloadFormattedError.set(
        document
          ? 'Save your CV sections before downloading a PDF.'
          : 'Create or upload a CV before downloading a PDF.'
      );
      return;
    }

    const fileName = this.buildFormattedExportFileName();

    this.cancelDownloadFormattedFile();
    this.downloadingFormatted.set(true);
    this.downloadFormattedError.set(null);

    this.downloadFormattedFileSubscription = this.apiService
      .downloadFormattedPdf()
      .subscribe({
        next: (result) => {
          this.downloadingFormatted.set(false);
          this.triggerDownload(result.blob, fileName);
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

  uploadProfilePhoto(file: File): void {
    if (!this.document()) {
      return;
    }

    this.cancelProfilePhotoUpload();
    this.uploadingProfilePhoto.set(true);
    this.profilePhotoError.set(null);
    this.clearExportHtmlPreview();

    this.profilePhotoUploadSubscription = this.apiService.uploadProfilePhoto(file).subscribe({
      next: (document) => {
        this.uploadingProfilePhoto.set(false);
        this.document.set(withDocumentDefaults(document));
        this.loadProfilePhoto(document);
      },
      error: (error) => {
        this.uploadingProfilePhoto.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.profilePhotoError.set(this.readErrorMessage(error, 'Could not upload your profile photo.'));
      }
    });
  }

  deleteProfilePhoto(): void {
    if (!this.document()?.hasProfilePhoto) {
      return;
    }

    this.cancelProfilePhotoDelete();
    this.deletingProfilePhoto.set(true);
    this.profilePhotoError.set(null);
    this.clearExportHtmlPreview();

    this.profilePhotoDeleteSubscription = this.apiService.deleteProfilePhoto().subscribe({
      next: (document) => {
        this.deletingProfilePhoto.set(false);
        this.document.set(withDocumentDefaults(document));
        this.clearProfilePhoto();
      },
      error: (error) => {
        this.deletingProfilePhoto.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.profilePhotoError.set(this.readErrorMessage(error, 'Could not remove your profile photo.'));
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

  private canExportFormatted(): boolean {
    const document = this.document();

    if (!document) {
      return false;
    }

    if (document.hasStructuredContent) {
      return true;
    }

    return (this.cvStructured.structured()?.sections.length ?? 0) > 0;
  }

  private clearExportHtmlPreview(): void {
    this.exportHtmlPreviewGeneration++;
    this.cancelExportHtmlPreview();
    this.exportHtmlPreviewLoading.set(false);
    this.exportHtmlPreviewError.set(null);
    this.exportHtmlPreviewSrcdoc.set(null);
    this.exportHtmlPreviewNotice.set(null);
    this.exportHtmlPreviewCompactLevel.set(null);
  }

  private resetState(): void {
    this.cancelLoad();
    this.cancelUpload();
    this.cancelStartBlank();
    this.cancelDownloadFormattedFile();
    this.cancelExportHtmlPreview();
    this.cancelProfilePhotoUpload();
    this.cancelProfilePhotoDelete();
    this.cancelUnplaced();
    this.cancelDelete();
    this.clearExportHtmlPreview();
    this.clearProfilePhoto();
    this.loading.set(false);
    this.uploading.set(false);
    this.startingBlank.set(false);
    this.downloadingFormatted.set(false);
    this.exportHtmlPreviewLoading.set(false);
    this.uploadingProfilePhoto.set(false);
    this.deletingProfilePhoto.set(false);
    this.unplacedBusy.set(false);
    this.unplacedError.set(null);
    this.deleting.set(false);
    this.deleteError.set(null);
    this.document.set(null);
    this.importSummary.set(null);
    this.error.set(null);
    this.uploadError.set(null);
    this.startBlankError.set(null);
    this.downloadFormattedError.set(null);
    this.exportHtmlPreviewError.set(null);
  }

  private cancelLoad(): void {
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = null;
  }

  private cancelUpload(): void {
    this.uploadSubscription?.unsubscribe();
    this.uploadSubscription = null;
  }

  private cancelStartBlank(): void {
    this.startBlankSubscription?.unsubscribe();
    this.startBlankSubscription = null;
  }

  private cancelDownloadFormattedFile(): void {
    this.downloadFormattedFileSubscription?.unsubscribe();
    this.downloadFormattedFileSubscription = null;
  }

  private cancelExportHtmlPreview(): void {
    this.exportHtmlPreviewSubscription?.unsubscribe();
    this.exportHtmlPreviewSubscription = null;
  }

  private cancelProfilePhoto(): void {
    this.profilePhotoSubscription?.unsubscribe();
    this.profilePhotoSubscription = null;
  }

  private cancelProfilePhotoUpload(): void {
    this.profilePhotoUploadSubscription?.unsubscribe();
    this.profilePhotoUploadSubscription = null;
  }

  private cancelProfilePhotoDelete(): void {
    this.profilePhotoDeleteSubscription?.unsubscribe();
    this.profilePhotoDeleteSubscription = null;
  }

  private cancelUnplaced(): void {
    this.unplacedSubscription?.unsubscribe();
    this.unplacedSubscription = null;
  }

  private cancelDelete(): void {
    this.deleteSubscription?.unsubscribe();
    this.deleteSubscription = null;
  }

  private triggerDownload(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  private buildFormattedExportFileName(): string {
    const personName = resolveCvExportPersonName(this.cvStructured.structured()?.sections);
    return buildCvExportDownloadFileName(personName);
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

function withDocumentDefaults(document: CvDocument): CvDocument {
  return {
    ...document,
    unplacedContent: document.unplacedContent ?? []
  };
}

function withImportDefaults(summary: CvStructuredImportSummary): CvStructuredImportSummary {
  return {
    ...summary,
    unplacedCount: summary.unplacedCount ?? 0
  };
}
