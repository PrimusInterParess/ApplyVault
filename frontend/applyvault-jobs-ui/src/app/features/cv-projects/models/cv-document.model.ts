export interface CvDocument {
  readonly id: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly fileSizeBytes: number;
  readonly uploadedAt: string;
}
