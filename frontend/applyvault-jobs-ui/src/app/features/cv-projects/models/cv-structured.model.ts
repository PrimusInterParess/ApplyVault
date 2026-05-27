export type CvSectionType =
  | 'Experience'
  | 'Projects'
  | 'Education'
  | 'Skills'
  | 'Summary'
  | 'Custom';

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
