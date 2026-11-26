export const MAX_PREPARATION_DAYS = 90;

export interface PreparationPackInput {
  readonly scrapeResultId: string | null;
  readonly targetRole: string;
  readonly targetLevel: string;
  readonly language: string;
  readonly hiringMarket: string;
  readonly preparationDays: number;
  readonly minutesPerDay: number;
  readonly interviewFormats: readonly string[];
  readonly knownWeaknesses: string;
  readonly additionalInformation: string;
  readonly projectSummaryIds: readonly string[];
}

export const defaultPreparationInput = (): PreparationPackInput => ({
  scrapeResultId: null, targetRole: '', targetLevel: '', language: 'english', hiringMarket: '',
  preparationDays: 14, minutesPerDay: 60, interviewFormats: [], knownWeaknesses: '',
  additionalInformation: '', projectSummaryIds: []
});

export interface PreparationDocument {
  readonly id: string;
  readonly title: string;
  readonly purpose: string;
  readonly markdown: string;
  readonly prerequisiteIds: readonly string[];
  readonly isGenerated?: boolean;
  readonly contextRevision?: number;
  readonly sourceFingerprint?: string | null;
}

export interface PreparationTopic {
  readonly name: string;
  readonly relevance: string;
  readonly evidence: string;
  readonly documentId: string;
}

export interface PreparationStudySession {
  readonly day: number;
  readonly minutes: number;
  readonly activity: string;
  readonly documentIds: readonly string[];
}

export interface PreparationPackContent {
  readonly title: string;
  readonly overviewMarkdown: string;
  readonly unknowns: readonly string[];
  readonly topics: readonly PreparationTopic[];
  readonly documents: readonly PreparationDocument[];
  readonly studyPlan: readonly PreparationStudySession[];
}

export interface PreparationProgress {
  readonly readDocumentIds: readonly string[];
  readonly explainedDocumentIds: readonly string[];
  readonly demonstratedDocumentIds: readonly string[];
  readonly adaptedDocumentIds: readonly string[];
  readonly completedDays: readonly number[];
  readonly reviewLog: string;
}

export const emptyPreparationProgress = (): PreparationProgress => ({
  readDocumentIds: [], explainedDocumentIds: [], demonstratedDocumentIds: [],
  adaptedDocumentIds: [], completedDays: [], reviewLog: ''
});

export type PreparationReadinessKey = 'readDocumentIds' | 'explainedDocumentIds' | 'demonstratedDocumentIds' | 'adaptedDocumentIds';

export interface PreparationPack {
  readonly id: string;
  readonly scrapeResultId: string | null;
  readonly jobTitle: string | null;
  readonly companyName: string | null;
  readonly input: PreparationPackInput;
  readonly content: PreparationPackContent | null;
  readonly progress: PreparationProgress;
  readonly needsRegeneration: boolean;
  readonly outdatedReasons: readonly string[];
  readonly isDemo: boolean;
  readonly generatedAt: string;
  readonly updatedAt: string;
  readonly discovery?: PreparationDiscovery | null;
}

export interface PreparationContext {
  readonly hasStructuredCv: boolean;
  readonly projects: readonly { readonly id: string; readonly title: string; readonly repository: string }[];
}

export type PreparationAnswerStatus = 'pending' | 'answered' | 'unknown' | 'skipped';
export interface PreparationDiscoveryQuestion {
  readonly id: string;
  readonly kind: 'knowledge' | 'experience' | 'preference';
  readonly prompt: string;
  readonly whyItMatters: string;
  readonly guidanceMarkdown: string;
  readonly answerStarter: string;
  readonly hypotheticalExample: string;
}
export interface PreparationDocumentOutline {
  readonly id: string;
  readonly title: string;
  readonly purpose: string;
  readonly prerequisiteIds: readonly string[];
  readonly questions: readonly PreparationDiscoveryQuestion[];
  readonly contextRevision?: number;
}
export interface PreparationAnswerInput {
  readonly questionId: string;
  readonly status: PreparationAnswerStatus;
  readonly text: string;
}
export interface PreparationDiscoveryAnswer extends PreparationAnswerInput {
  readonly documentId: string;
}
export interface PreparationDiscovery {
  readonly documents: readonly PreparationDocumentOutline[];
  readonly answers: readonly PreparationDiscoveryAnswer[];
  readonly contextRevision: number;
}
export interface PreparationHelpResponse {
  readonly markdown: string;
  readonly isDemo: boolean;
}
