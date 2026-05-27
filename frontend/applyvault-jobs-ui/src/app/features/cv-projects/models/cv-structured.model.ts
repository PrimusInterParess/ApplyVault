export const CV_SECTION_TYPES = [
  'Experience',
  'Projects',
  'Education',
  'Skills',
  'Summary',
  'Custom'
] as const;

export type CvSectionType = (typeof CV_SECTION_TYPES)[number];

export interface CvStructuredEntry {
  readonly id: string;
  title: string;
  subtitle: string | null;
  dateRange: string | null;
  summary: string;
  bullets: string[];
  techStack: string;
  source: string;
  sourceSummaryId: string | null;
  sortOrder: number;
}

export interface CvStructuredSection {
  readonly id: string;
  heading: string;
  sectionType: CvSectionType;
  sortOrder: number;
  entries: CvStructuredEntry[];
}

export interface CvStructuredDocument {
  readonly documentId: string;
  structuredImportedAt: string | null;
  sections: CvStructuredSection[];
}

export interface CvStructuredSectionWrite {
  id: string | null;
  heading: string;
  sectionType: CvSectionType;
  sortOrder: number;
  entries: CvStructuredEntryWrite[];
}

export interface CvStructuredEntryWrite {
  id: string | null;
  title: string;
  subtitle: string | null;
  dateRange: string | null;
  summary: string;
  bullets: string[];
  techStack: string;
  source: string;
  sourceSummaryId: string | null;
  sortOrder: number;
}

export interface CvStructuredImportPreview {
  sections: CvStructuredSectionWrite[];
  usedAi: boolean;
  notice: string | null;
}

export interface SaveCvStructuredDocumentRequest {
  readonly sections: readonly CvStructuredSectionWrite[];
}

export interface InsertCvEntryFromSummaryRequest {
  readonly summaryId: string;
}
