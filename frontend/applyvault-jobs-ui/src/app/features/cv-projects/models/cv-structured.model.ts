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

export interface CvStructuredSectionWrite {
  id: string | null;
  heading: string;
  sectionType: CvSectionType;
  sortOrder: number;
  entries: CvStructuredEntryWrite[];
}

export interface SaveCvStructuredDocumentRequest {
  sections: CvStructuredSectionWrite[];
}

export interface UpdateCvStructuredWithAiRequest {
  instructions: string;
  sectionIds?: string[];
}

export interface GenerateCvImprovementSuggestionsRequest {
  sectionIds?: string[];
  maxSuggestions: number;
}

export interface CvImprovementSuggestions {
  readonly documentId: string;
  structuredImportedAt: string | null;
  suggestions: CvImprovementSuggestion[];
}

export interface CvImprovementSuggestion {
  readonly id: string;
  title: string;
  rationale: string;
  suggestedInstruction: string;
  sectionId: string | null;
  entryId: string | null;
  category: string;
  impact: string;
}

export const CV_SECTION_TYPES: readonly CvSectionType[] = [
  'Experience',
  'Projects',
  'Education',
  'Skills',
  'Summary',
  'Custom'
] as const;
