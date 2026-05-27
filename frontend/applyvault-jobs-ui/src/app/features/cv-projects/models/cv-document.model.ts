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
}
