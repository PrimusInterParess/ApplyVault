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
