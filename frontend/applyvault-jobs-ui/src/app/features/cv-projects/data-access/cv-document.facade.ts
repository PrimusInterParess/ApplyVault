import { HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { DomSanitizer, SafeHtml, SafeResourceUrl } from '@angular/platform-browser';
import { Subscription } from 'rxjs';
import { switchMap, map } from 'rxjs/operators';

import { AuthService } from '../../../core/auth/auth.service';
import { isRequestAborted } from '../../../core/http/is-request-aborted';
import { CvDocument, CvStructuredImportSummary } from '../models/cv-document.model';
import {
  CV_EXPORT_MAX_PAGES_STORAGE_KEY,
  CV_EXPORT_TEMPLATE_STORAGE_KEY,
  DEFAULT_CV_EXPORT_MAX_PAGES,
  DEFAULT_CV_EXPORT_TEMPLATE_ID,
  normalizeCvExportTemplateId
} from '../models/cv-export-template.model';
import { CvDocumentApiService } from './cv-document-api.service';
import { CvStructuredFacade } from './cv-structured.facade';
import { createBuilderStarterSections } from '../utils/cv-builder-starter-sections.util';
import { hydrateStructuredDocument, toSaveRequest } from '../utils/cv-structured-draft.util';

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
  private startBlankSubscription: Subscription | null = null;
  private downloadOriginalSubscription: Subscription | null = null;
  private downloadFormattedSubscription: Subscription | null = null;
  private downloadFormattedFileSubscription: Subscription | null = null;
  private exportHtmlPreviewSubscription: Subscription | null = null;
  private exportPrefsSubscription: Subscription | null = null;
  private profilePhotoSubscription: Subscription | null = null;
  private profilePhotoUploadSubscription: Subscription | null = null;
  private profilePhotoDeleteSubscription: Subscription | null = null;
  private loadedUserId: string | null = null;
  private profilePhotoObjectUrl: string | null = null;
  private previewObjectUrl: string | null = null;
  private previewBlob: Blob | null = null;

  readonly loading = signal(false);
  readonly uploading = signal(false);
  readonly reimporting = signal(false);
  readonly deleting = signal(false);
  readonly startingBlank = signal(false);
  readonly downloadingOriginal = signal(false);
  readonly downloadingFormatted = signal(false);
  readonly previewLoading = signal(false);
  readonly exportHtmlPreviewLoading = signal(false);
  readonly loadingProfilePhoto = signal(false);
  readonly uploadingProfilePhoto = signal(false);
  readonly deletingProfilePhoto = signal(false);
  readonly document = signal<CvDocument | null>(null);
  readonly importSummary = signal<CvStructuredImportSummary | null>(null);
  readonly error = signal<string | null>(null);
  readonly uploadError = signal<string | null>(null);
  readonly reimportError = signal<string | null>(null);
  readonly deleteError = signal<string | null>(null);
  readonly startBlankError = signal<string | null>(null);
  readonly downloadOriginalError = signal<string | null>(null);
  readonly downloadFormattedError = signal<string | null>(null);
  readonly previewError = signal<string | null>(null);
  readonly exportHtmlPreviewError = signal<string | null>(null);
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
  /** Sandboxed iframe srcdoc for M1 fidelity preview (strategy A). */
  readonly exportHtmlPreviewSrcdoc = signal<SafeHtml | null>(null);

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
      this.clearExportHtmlPreview();
    });
  }

  load(): void {
    this.cancelLoad();
    this.loading.set(true);
    this.error.set(null);

    this.loadSubscription = this.apiService.getCurrent().subscribe({
      next: (document) => {
        this.loading.set(false);
        this.setDocument(document);
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
    this.clearFormattedPreview();
    this.clearProfilePhoto();

    this.uploadSubscription = this.apiService
      .upload(file)
      .pipe(
        switchMap((result) => {
          this.setDocument(result.document);
          this.importSummary.set(result.import);
          this.loadProfilePhoto(result.document);
          return this.apiService.getStructured().pipe(
            map((structured) => ({ result, structured }))
          );
        })
      )
      .subscribe({
        next: ({ structured }) => {
          this.uploading.set(false);
          this.cvStructured.setStructured(hydrateStructuredDocument(structured));
          this.persistExportPrefs();
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

  startBlank(): void {
    this.cancelStartBlank();
    this.startingBlank.set(true);
    this.startBlankError.set(null);
    this.importSummary.set(null);
    this.clearFormattedPreview();
    this.clearProfilePhoto();

    this.startBlankSubscription = this.apiService.startBlank().subscribe({
      next: (document) => {
        this.startingBlank.set(false);
        this.setDocument(document);
        this.cvStructured.setStructured({
          documentId: document.id,
          structuredImportedAt: null,
          sections: []
        });
        this.persistExportPrefs();
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

  startBlankWithStarterSections(onComplete?: () => void): void {
    this.cancelStartBlank();
    this.startingBlank.set(true);
    this.startBlankError.set(null);
    this.importSummary.set(null);
    this.clearFormattedPreview();
    this.clearProfilePhoto();

    this.startBlankSubscription = this.apiService
      .startBlank()
      .pipe(
        switchMap((document) => {
          this.setDocument(document);
          return this.apiService.saveStructured(toSaveRequest(createBuilderStarterSections()));
        })
      )
      .subscribe({
        next: (structured) => {
          this.startingBlank.set(false);
          this.cvStructured.setStructured(hydrateStructuredDocument(structured));
          const current = this.document();

          if (current) {
            this.document.set({
              ...current,
              hasStructuredContent: structured.sections.length > 0,
              structuredImportedAt: structured.structuredImportedAt
            });
          }

          this.persistExportPrefs();
          this.load();
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
    const normalized = normalizeCvExportTemplateId(templateId);
    this.selectedExportTemplateId.set(normalized);
    this.clearFormattedPreview();
    this.clearExportHtmlPreview();
    this.writeTemplateCache(normalized);
    this.persistExportPrefs();
  }

  setExportMaxPages(maxPages: number | null): void {
    const normalized = this.normalizeExportMaxPages(maxPages);
    this.selectedExportMaxPages.set(normalized);
    this.clearFormattedPreview();
    this.clearExportHtmlPreview();
    this.writeMaxPagesCache(normalized);
    this.persistExportPrefs();
  }

  /**
   * Load server HTML for the sandboxed fidelity iframe.
   * Same templateId + maxPages as PDF download so compact CSS matches export.
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

    const templateId = this.selectedExportTemplateId();
    const maxPages = this.selectedExportMaxPages();

    this.cancelExportHtmlPreview();
    this.exportHtmlPreviewLoading.set(true);
    this.exportHtmlPreviewError.set(null);
    this.exportHtmlPreviewSrcdoc.set(null);

    this.exportHtmlPreviewSubscription = this.apiService
      .getExportPreviewHtml({ templateId, maxPages })
      .subscribe({
        next: (html) => {
          this.exportHtmlPreviewLoading.set(false);
          this.exportHtmlPreviewSrcdoc.set(this.sanitizer.bypassSecurityTrustHtml(html));
        },
        error: (error) => {
          this.exportHtmlPreviewLoading.set(false);
          this.exportHtmlPreviewSrcdoc.set(null);

          if (isRequestAborted(error)) {
            return;
          }

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

  downloadFormatted(): void {
    this.previewFormatted();
  }

  /** Fetch and save the formatted PDF to disk (does not require the preview modal). */
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

    const templateId = this.selectedExportTemplateId();
    const maxPages = this.selectedExportMaxPages();
    const baseName = document!.originalFileName.replace(/\.pdf$/i, '');

    this.cancelDownloadFormattedFile();
    this.downloadingFormatted.set(true);
    this.downloadFormattedError.set(null);

    this.downloadFormattedFileSubscription = this.apiService
      .downloadFormattedPdf({ templateId, maxPages })
      .subscribe({
        next: (result) => {
          this.downloadingFormatted.set(false);
          this.triggerDownload(result.blob, `${baseName}-export.pdf`);
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

  previewFormatted(): void {
    const document = this.document();

    if (!this.canExportFormatted()) {
      this.previewError.set(
        document
          ? 'Save your CV sections before previewing a PDF.'
          : 'Create or upload a CV before previewing a PDF.'
      );
      this.downloadFormattedError.set(this.previewError());
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

  uploadProfilePhoto(file: File): void {
    if (!this.document()) {
      return;
    }

    this.cancelProfilePhotoUpload();
    this.uploadingProfilePhoto.set(true);
    this.profilePhotoError.set(null);
    this.clearFormattedPreview();
    this.clearExportHtmlPreview();

    this.profilePhotoUploadSubscription = this.apiService.uploadProfilePhoto(file).subscribe({
      next: (document) => {
        this.uploadingProfilePhoto.set(false);
        this.setDocument(document);
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
    this.clearFormattedPreview();
    this.clearExportHtmlPreview();

    this.profilePhotoDeleteSubscription = this.apiService.deleteProfilePhoto().subscribe({
      next: (document) => {
        this.deletingProfilePhoto.set(false);
        this.setDocument(document);
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

  private setFormattedPreviewBlob(blob: Blob): void {
    this.clearFormattedPreviewBlob();
    this.previewBlob = blob;
    this.previewObjectUrl = URL.createObjectURL(blob);
    this.previewBlobUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.previewObjectUrl));
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

  private clearExportHtmlPreview(): void {
    this.cancelExportHtmlPreview();
    this.exportHtmlPreviewLoading.set(false);
    this.exportHtmlPreviewError.set(null);
    this.exportHtmlPreviewSrcdoc.set(null);
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
    this.cancelStartBlank();
    this.cancelDownloadOriginal();
    this.cancelDownloadFormatted();
    this.cancelDownloadFormattedFile();
    this.cancelExportHtmlPreview();
    this.cancelExportPrefs();
    this.cancelProfilePhotoUpload();
    this.cancelProfilePhotoDelete();
    this.clearFormattedPreview();
    this.clearExportHtmlPreview();
    this.clearProfilePhoto();
    this.loading.set(false);
    this.uploading.set(false);
    this.reimporting.set(false);
    this.deleting.set(false);
    this.startingBlank.set(false);
    this.downloadingOriginal.set(false);
    this.downloadingFormatted.set(false);
    this.previewLoading.set(false);
    this.exportHtmlPreviewLoading.set(false);
    this.uploadingProfilePhoto.set(false);
    this.deletingProfilePhoto.set(false);
    this.document.set(null);
    this.importSummary.set(null);
    this.error.set(null);
    this.uploadError.set(null);
    this.reimportError.set(null);
    this.deleteError.set(null);
    this.startBlankError.set(null);
    this.downloadOriginalError.set(null);
    this.downloadFormattedError.set(null);
    this.previewError.set(null);
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

  private cancelReimport(): void {
    this.reimportSubscription?.unsubscribe();
    this.reimportSubscription = null;
  }

  private cancelDelete(): void {
    this.deleteSubscription?.unsubscribe();
    this.deleteSubscription = null;
  }

  private cancelStartBlank(): void {
    this.startBlankSubscription?.unsubscribe();
    this.startBlankSubscription = null;
  }

  private cancelDownloadOriginal(): void {
    this.downloadOriginalSubscription?.unsubscribe();
    this.downloadOriginalSubscription = null;
  }

  private cancelDownloadFormatted(): void {
    this.downloadFormattedSubscription?.unsubscribe();
    this.downloadFormattedSubscription = null;
  }

  private cancelDownloadFormattedFile(): void {
    this.downloadFormattedFileSubscription?.unsubscribe();
    this.downloadFormattedFileSubscription = null;
  }

  private cancelExportHtmlPreview(): void {
    this.exportHtmlPreviewSubscription?.unsubscribe();
    this.exportHtmlPreviewSubscription = null;
  }

  private cancelExportPrefs(): void {
    this.exportPrefsSubscription?.unsubscribe();
    this.exportPrefsSubscription = null;
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

  private triggerDownload(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  /**
   * Apply document metadata and prefer server templateId/maxPages over sessionStorage.
   */
  private setDocument(document: CvDocument): void {
    this.document.set(document);
    this.applyExportPrefsFromDocument(document);
  }

  private applyExportPrefsFromDocument(document: CvDocument): void {
    if (typeof document.templateId === 'number' && Number.isInteger(document.templateId)) {
      const normalized = normalizeCvExportTemplateId(document.templateId);
      this.selectedExportTemplateId.set(normalized);
      this.writeTemplateCache(normalized);
    }

    // null = explicit no limit; server value wins over sessionStorage cache.
    if (document.maxPages !== undefined) {
      const normalized = this.normalizeExportMaxPages(document.maxPages);
      this.selectedExportMaxPages.set(normalized);
      this.writeMaxPagesCache(normalized);
    }
  }

  /** Persist current selection to API when a document exists; sessionStorage remains cache. */
  private persistExportPrefs(): void {
    if (!this.document()) {
      return;
    }

    const templateId = this.selectedExportTemplateId();
    const maxPages = this.selectedExportMaxPages();

    this.cancelExportPrefs();
    this.exportPrefsSubscription = this.apiService
      .updateExportPrefs({ templateId, maxPages })
      .subscribe({
        next: (document) => {
          // Update metadata without re-applying prefs (avoids echo loops on normalize).
          this.document.set(document);

          if (typeof document.templateId === 'number' && Number.isInteger(document.templateId)) {
            const normalized = normalizeCvExportTemplateId(document.templateId);

            if (normalized !== this.selectedExportTemplateId()) {
              this.selectedExportTemplateId.set(normalized);
              this.writeTemplateCache(normalized);
            }
          }

          if (document.maxPages !== undefined) {
            const normalized = this.normalizeExportMaxPages(document.maxPages);

            if (normalized !== this.selectedExportMaxPages()) {
              this.selectedExportMaxPages.set(normalized);
              this.writeMaxPagesCache(normalized);
            }
          }
        },
        error: (error) => {
          if (isRequestAborted(error)) {
            return;
          }

          // Soft-fail non-success; local + sessionStorage cache still drive export/preview.
          if (
            error instanceof HttpErrorResponse &&
            (error.status === 404 || error.status === 405 || error.status === 501)
          ) {
            return;
          }

          // Soft-fail other errors; export/preview still use in-memory selection.
        }
      });
  }

  private writeTemplateCache(templateId: number): void {
    try {
      sessionStorage.setItem(CV_EXPORT_TEMPLATE_STORAGE_KEY, String(templateId));
    } catch {
      // Ignore storage failures (private mode, quota, etc.).
    }
  }

  private writeMaxPagesCache(maxPages: number | null): void {
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

  private normalizeExportMaxPages(maxPages: number | null): number | null {
    if (maxPages === null) {
      return null;
    }

    return Number.isInteger(maxPages) && maxPages >= 1 && maxPages <= 2
      ? maxPages
      : DEFAULT_CV_EXPORT_MAX_PAGES;
  }

  private readStoredExportTemplateId(): number {
    try {
      const stored = sessionStorage.getItem(CV_EXPORT_TEMPLATE_STORAGE_KEY);

      if (!stored) {
        return DEFAULT_CV_EXPORT_TEMPLATE_ID;
      }

      const parsed = Number.parseInt(stored, 10);

      if (!Number.isInteger(parsed)) {
        return DEFAULT_CV_EXPORT_TEMPLATE_ID;
      }

      const normalized = normalizeCvExportTemplateId(parsed);

      // Persist remap so legacy ids 4/5 (and unknowns) become Classic in sessionStorage.
      if (normalized !== parsed) {
        this.writeTemplateCache(normalized);
      }

      return normalized;
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
      return this.normalizeExportMaxPages(Number.isInteger(parsed) ? parsed : null);
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
