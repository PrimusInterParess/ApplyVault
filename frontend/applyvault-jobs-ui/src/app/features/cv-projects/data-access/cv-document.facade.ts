import { HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Subscription } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';

import { AuthService } from '../../../core/auth/auth.service';
import { isRequestAborted } from '../../../core/http/is-request-aborted';
import { CvDocument, CvStructuredImportSummary } from '../models/cv-document.model';
import {
  CV_EXPORT_TEMPLATE_STORAGE_KEY,
  DEFAULT_CV_EXPORT_TEMPLATE_ID,
  normalizeCvExportTemplateId
} from '../models/cv-export-template.model';
import { CvDocumentApiService } from './cv-document-api.service';
import { CvStructuredFacade } from './cv-structured.facade';
import { createBuilderStarterSections } from '../utils/cv-builder-starter-sections.util';
import {
  buildCvExportDownloadFileName,
  resolveCvExportPersonName,
  resolveCvExportTemplateLabel
} from '../utils/cv-export-download-file-name.util';
import { toSaveRequest } from '../utils/cv-structured-draft.util';

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
  private exportPrefsSubscription: Subscription | null = null;
  /** Monotonic token so stale export-prefs HTTP responses cannot overwrite a newer selection. */
  private exportPrefsGeneration = 0;
  private profilePhotoSubscription: Subscription | null = null;
  private profilePhotoUploadSubscription: Subscription | null = null;
  private profilePhotoDeleteSubscription: Subscription | null = null;
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
  readonly profilePhotoUrl = signal<string | null>(null);
  readonly selectedExportTemplateId = signal(this.readStoredExportTemplateId());
  /** Sandboxed iframe srcdoc for Check-export fidelity preview. */
  readonly exportHtmlPreviewSrcdoc = signal<SafeHtml | null>(null);
  /** Additive preview notice from X-Cv-Export-Notice (canvas). */
  readonly exportHtmlPreviewNotice = signal<string | null>(null);
  /** Additive compact level from X-Cv-Export-Compact-Level (canvas). */
  readonly exportHtmlPreviewCompactLevel = signal<number | null>(null);

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
    this.clearProfilePhoto();

    this.uploadSubscription = this.apiService
      .upload(file)
      .pipe(
        switchMap((result) => {
          // Keep optimistic gallery/edit Template selection; upload DTO may echo a stale id.
          this.setDocument(result.document, { applyExportPrefs: false });
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
          // Raw DTO — setStructured / hydrateForContentEditing is the sole hydrate+normalize ingress.
          this.cvStructured.setStructured(structured);
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

  startBlankWithStarterSections(onComplete?: () => void): void {
    this.cancelStartBlank();
    this.startingBlank.set(true);
    this.startBlankError.set(null);
    this.importSummary.set(null);
    this.clearProfilePhoto();

    this.startBlankSubscription = this.apiService
      .startBlank()
      .pipe(
        switchMap((document) => {
          // Keep optimistic gallery/edit Template selection; blank docs default to Modern.
          this.setDocument(document, { applyExportPrefs: false });
          return this.apiService.saveStructured(toSaveRequest(createBuilderStarterSections()));
        })
      )
      .subscribe({
        next: (structured) => {
          this.startingBlank.set(false);
          // Raw DTO — setStructured / hydrateForContentEditing is the sole hydrate+normalize ingress.
          this.cvStructured.setStructured(structured);
          const current = this.document();

          if (current) {
            this.document.set({
              ...current,
              hasStructuredContent: structured.sections.length > 0,
              structuredImportedAt: structured.structuredImportedAt,
              templateId: this.selectedExportTemplateId()
            });
          }

          this.persistExportPrefs();
          // Do not load() here — getCurrent would re-apply server TemplateId (often Modern)
          // and snap the edit/gallery selection before persistExportPrefs completes.
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

  setExportTemplateId(templateId: number): void {
    const normalized = normalizeCvExportTemplateId(templateId);
    this.selectedExportTemplateId.set(normalized);
    // Keep last HTML srcdoc until refreshExportHtmlPreview replaces it.
    this.writeTemplateCache(normalized);
    this.persistExportPrefs();
  }

  /**
   * Load server HTML for the sandboxed fidelity iframe.
   * Same templateId as PDF download so BE compact CSS matches export.
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

    const previewGeneration = ++this.exportHtmlPreviewGeneration;
    this.cancelExportHtmlPreview();
    this.exportHtmlPreviewLoading.set(true);
    this.exportHtmlPreviewError.set(null);
    // Keep last srcdoc/notice/compact until the next successful response (no blank canvas).

    this.exportHtmlPreviewSubscription = this.apiService
      .getExportPreviewHtml({ templateId })
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

    const templateId = this.selectedExportTemplateId();
    const fileName = this.buildFormattedExportFileName(templateId);

    this.cancelDownloadFormattedFile();
    this.downloadingFormatted.set(true);
    this.downloadFormattedError.set(null);

    this.downloadFormattedFileSubscription = this.apiService
      .downloadFormattedPdf({ templateId })
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
        // Photo DTO must not clobber in-flight / optimistic export template selection.
        this.setDocument(document, { applyExportPrefs: false });
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
        // Photo DTO must not clobber in-flight / optimistic export template selection.
        this.setDocument(document, { applyExportPrefs: false });
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
    this.cancelExportPrefs();
    this.cancelProfilePhotoUpload();
    this.cancelProfilePhotoDelete();
    this.clearExportHtmlPreview();
    this.clearProfilePhoto();
    this.loading.set(false);
    this.uploading.set(false);
    this.startingBlank.set(false);
    this.downloadingFormatted.set(false);
    this.exportHtmlPreviewLoading.set(false);
    this.uploadingProfilePhoto.set(false);
    this.deletingProfilePhoto.set(false);
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

  private buildFormattedExportFileName(templateId: number): string {
    const personName = resolveCvExportPersonName(this.cvStructured.structured()?.sections);
    const templateLabel = resolveCvExportTemplateLabel(templateId);
    return buildCvExportDownloadFileName(personName, templateLabel);
  }

  /**
   * Apply document metadata. Optionally sync export prefs from the DTO (load/upload/start-blank).
   * Photo and export-prefs echoes must not clobber an optimistic gallery selection.
   */
  private setDocument(
    document: CvDocument,
    options?: { readonly applyExportPrefs?: boolean }
  ): void {
    this.document.set(document);

    if (options?.applyExportPrefs === false) {
      return;
    }

    this.applyExportPrefsFromDocument(document);
  }

  private applyExportPrefsFromDocument(document: CvDocument): void {
    // In-flight persistExportPrefs owns Template selection — do not snap back from a
    // stale getCurrent / start-blank / upload echo (server default is a common culprit).
    const prefsWriteInFlight =
      this.exportPrefsSubscription !== null && !this.exportPrefsSubscription.closed;

    if (
      !prefsWriteInFlight &&
      typeof document.templateId === 'number' &&
      Number.isInteger(document.templateId)
    ) {
      const normalized = normalizeCvExportTemplateId(document.templateId);
      this.selectedExportTemplateId.set(normalized);
      this.writeTemplateCache(normalized);
    }
  }

  /** Persist current selection to API when a document exists; sessionStorage remains cache. */
  private persistExportPrefs(): void {
    if (!this.document()) {
      return;
    }

    const templateId = this.selectedExportTemplateId();
    const generation = ++this.exportPrefsGeneration;

    this.cancelExportPrefs();
    this.exportPrefsSubscription = this.apiService
      .updateExportPrefs({ templateId })
      .subscribe({
        next: (document) => {
          // Ignore superseded responses (rapid template switching / cancelled request races).
          if (generation !== this.exportPrefsGeneration) {
            return;
          }

          // Refresh document metadata but keep optimistic export prefs as source of truth.
          // Re-applying response templateId previously snapped the edit canvas back when the
          // echo lagged or differed from the latest local selection.
          this.document.set({
            ...document,
            templateId: this.selectedExportTemplateId()
          });
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

      // Persist remap so legacy Classic/4/5 (and unknowns) become Modern in sessionStorage.
      if (normalized !== parsed) {
        this.writeTemplateCache(normalized);
      }

      return normalized;
    } catch {
      return DEFAULT_CV_EXPORT_TEMPLATE_ID;
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
