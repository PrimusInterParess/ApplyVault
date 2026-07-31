import { CvStructuredDocument } from './cv-structured.model';

export interface CvDocument {
  readonly id: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly fileSizeBytes: number;
  readonly originalFileSizeBytes: number;
  readonly uploadedAt: string;
  readonly hasMergedProjects: boolean;
  readonly hasStructuredContent: boolean;
  readonly structuredImportedAt: string | null;
  readonly hasProfilePhoto: boolean;
  readonly hasOriginalUpload: boolean;
  /** M2: persisted export Template id (normalized 1|2|3). Server wins over sessionStorage on load. */
  readonly templateId: number;
  /** M2: persisted max page limit (null = no limit; BE allows 1–5 when set). */
  readonly maxPages: number | null;
}

/** PUT /api/cv-documents/current/export-preferences */
export interface UpdateCvExportPrefsRequest {
  readonly templateId: number;
  readonly maxPages: number | null;
}

export interface CvStructuredImportSummary {
  readonly succeeded: boolean;
  readonly sectionCount: number;
  readonly usedAi: boolean;
  readonly profilePhotoExtracted: boolean;
  readonly notice: string | null;
}

export interface CvDocumentUploadResult {
  readonly document: CvDocument;
  readonly import: CvStructuredImportSummary;
}

export interface CvStructuredReimportResult {
  readonly structured: CvStructuredDocument | null;
  readonly import: CvStructuredImportSummary;
}
