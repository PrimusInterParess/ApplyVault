export type CvSectionType =
  | 'Experience'
  | 'Projects'
  | 'Education'
  | 'Skills'
  | 'Summary'
  | 'Contact'
  | 'Custom';

export type CvEntryFieldKind = 'string' | 'text' | 'stringList';

export interface CvSectionFieldCatalog {
  readonly id: string;
  readonly label: string;
  readonly kind: CvEntryFieldKind;
}

export interface CvSectionTypeCatalog {
  readonly id: CvSectionType;
  readonly defaultHeading: string;
  readonly headingAliases: readonly string[];
  readonly entryFields: readonly CvSectionFieldCatalog[];
  readonly importHints?: string | null;
}

export interface CvSectionCatalogDocument {
  readonly version: number;
  readonly sectionTypes: readonly CvSectionTypeCatalog[];
}

export interface CvStructuredEntry {
  readonly id: string;
  title: string;
  subtitle: string | null;
  dateRange: string | null;
  summary: string;
  bullets: string[];
  techStack: string;
  fields: Record<string, unknown>;
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

/** Request for ephemeral CV quality evaluation (no JD; not persisted). */
export interface EvaluateCvQualityRequest {
  maxFindings?: number;
}

/** Request for ephemeral Summary regeneration proposal (instructions optional). */
export interface ProposeCvSummaryRequest {
  instructions?: string;
}

/**
 * Ephemeral Summary proposal — session UI state only.
 * Approve patches Summary locally + existing save; never persist via ai-update.
 */
export interface CvSummaryProposal {
  readonly documentId: string;
  summarySectionId: string | null;
  currentSummaryText: string;
  proposedSummaryText: string;
  changeBullets: string[];
}

/**
 * Ephemeral multi-section Update proposal — session UI state only.
 * Approve merges proposed sections locally + existing save; never ai-update.
 */
export interface CvUpdateProposal {
  readonly documentId: string;
  focusSectionIds: string[];
  changeBullets: string[];
  proposedSections: CvStructuredSection[];
}

export type CvQualityEvaluationDimensionId = 'content' | 'structure' | 'format';

export type CvQualityEvaluationSeverity = 'info' | 'warning' | 'critical';

export interface CvQualityEvaluationDimension {
  readonly id: string;
  score: number;
  summary: string;
}

export interface CvQualityEvaluationFinding {
  readonly id: string;
  dimension: string;
  severity: string;
  title: string;
  detail: string;
  sectionId: string | null;
  entryId: string | null;
}

/** Ephemeral evaluation response — session UI state only (D2: do not persist). */
export interface CvQualityEvaluation {
  readonly documentId: string;
  overallScore: number;
  summary: string;
  dimensions: CvQualityEvaluationDimension[];
  findings: CvQualityEvaluationFinding[];
  selfCheckQuestions: string[];
}

export const CV_SECTION_TYPES: readonly CvSectionType[] = [
  'Experience',
  'Projects',
  'Education',
  'Skills',
  'Summary',
  'Custom'
] as const;
