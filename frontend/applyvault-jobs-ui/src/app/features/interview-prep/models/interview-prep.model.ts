export type InterviewPrepMode =
  | 'screeningAndMotivation'
  | 'behavioralAndCulture'
  | 'roleAndDomainDepth'
  | 'processAndSystems'
  | 'problemSolvingCase'
  | 'languagePractice'
  | 'fullLoop';

export type InterviewPrepPersona =
  | 'recruiter'
  | 'hiringManager'
  | 'seniorPeer'
  | 'barRaiser';

export type InterviewPrepLanguage = 'english' | 'danish' | 'mixedEnglishDanish';

export type InterviewPrepMarket = 'general' | 'danish';

export type InterviewPrepExperienceType = 'realisticSimulation' | 'guidedCoaching';

export type InterviewPrepInteractionType = 'text';

export type InterviewPrepSessionStatus =
  | 'created'
  | 'preparing'
  | 'ready'
  | 'inProgress'
  | 'paused'
  | 'completing'
  | 'completed'
  | 'cancelled'
  | 'failed';

export type InterviewPrepStageStatus =
  | 'planned'
  | 'opening'
  | 'warmUp'
  | 'coreAssessment'
  | 'candidateQuestions'
  | 'closing'
  | 'assessmentPending'
  | 'assessed'
  | 'completed';

export type InterviewPrepTurnRole = 'system' | 'interviewer' | 'candidate' | 'coach';

export interface InterviewPrepModeOption {
  readonly id: InterviewPrepMode;
  readonly label: string;
  readonly description: string;
}

export interface InterviewPrepPersonaOption {
  readonly id: InterviewPrepPersona;
  readonly label: string;
}

export interface InterviewPrepLanguageOption {
  readonly id: InterviewPrepLanguage;
  readonly label: string;
}

export interface InterviewPrepMarketOption {
  readonly id: InterviewPrepMarket;
  readonly label: string;
}

export interface InterviewPrepExperienceTypeOption {
  readonly id: InterviewPrepExperienceType;
  readonly label: string;
  readonly description: string;
}

export interface InterviewPrepCreateSessionRequest {
  readonly mode: InterviewPrepMode;
  readonly persona: InterviewPrepPersona;
  readonly language: InterviewPrepLanguage;
  readonly market: InterviewPrepMarket;
  readonly experienceType: InterviewPrepExperienceType;
  readonly interactionType: InterviewPrepInteractionType;
  readonly scrapeResultId?: string | null;
  readonly idempotencyKey?: string | null;
}

export interface InterviewPrepSubmitTurnRequest {
  readonly clientTurnId: string;
  readonly answer: string;
}

export interface InterviewPrepSessionSummary {
  readonly id: string;
  readonly status: InterviewPrepSessionStatus | string;
  readonly mode: InterviewPrepMode | string;
  readonly persona: InterviewPrepPersona | string;
  readonly language: InterviewPrepLanguage | string;
  readonly market: InterviewPrepMarket | string;
  readonly experienceType: InterviewPrepExperienceType | string;
  readonly interactionType: InterviewPrepInteractionType | string;
  readonly scrapeResultId: string | null;
  readonly jobTitle: string | null;
  readonly companyName: string | null;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly preparedAt: string | null;
  readonly startedAt: string | null;
  readonly completedAt: string | null;
  readonly eTag: string;
}

export interface InterviewPrepSessionListResponse {
  readonly items: readonly InterviewPrepSessionSummary[];
}

export interface InterviewPrepStage {
  readonly id: string;
  readonly sortOrder: number;
  readonly stageType: string;
  readonly status: InterviewPrepStageStatus | string;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly completedAt: string | null;
}

/** Wire action types for interviewer turns (camelCase). */
export type InterviewPrepTurnActionType =
  | 'opening'
  | 'askQuestion'
  | 'probe'
  | 'candidateQuestions'
  | 'wrapUp'
  | 'close'
  | 'stageHandoff'
  | 'discloseFact'
  | 'offerHint'
  | 'introduceComplication'
  | string;

export interface InterviewPrepTurn {
  readonly id: string;
  readonly stageId: string;
  readonly sequence: number;
  readonly role: InterviewPrepTurnRole | string;
  readonly text: string;
  readonly questionSignature: string | null;
  readonly competencyTag: string | null;
  readonly language: string | null;
  readonly clientTurnId: string | null;
  readonly createdAt: string;
  /** Present when API includes turn actionType (e.g. stageHandoff). */
  readonly actionType?: InterviewPrepTurnActionType | null;
}

export interface InterviewPrepBriefUnknown {
  readonly signal: string;
  readonly coverageState: string;
}

export interface InterviewPrepBrief {
  readonly summary: string;
  readonly themes: readonly string[];
  readonly risks: readonly string[];
  readonly talkingPoints: readonly string[];
  readonly unknowns: readonly InterviewPrepBriefUnknown[];
  readonly presentCvSectionTypes: readonly string[];
  readonly jobTitle: string | null;
  readonly companyName: string | null;
  readonly source: string;
  readonly usedAiFallback: boolean;
}

export interface InterviewPrepPlanBudgets {
  /** Soft Main-question target (~8–12; catalog default ~10). */
  readonly targetQuestions: number;
  /** Hard Main-question safety (~15–18; catalog default ~16). */
  readonly maxQuestions: number;
  readonly maxProbes: number;
  readonly maxTurns: number;
}

export interface InterviewPrepPlan {
  readonly planSummary: string;
  readonly source: string;
  readonly usedAiFallback: boolean;
  readonly budgets?: InterviewPrepPlanBudgets | null;
}

export interface InterviewPrepSessionDetail {
  readonly id: string;
  readonly status: InterviewPrepSessionStatus | string;
  readonly mode: InterviewPrepMode | string;
  readonly persona: InterviewPrepPersona | string;
  readonly language: InterviewPrepLanguage | string;
  readonly market: InterviewPrepMarket | string;
  readonly experienceType: InterviewPrepExperienceType | string;
  readonly interactionType: InterviewPrepInteractionType | string;
  readonly scrapeResultId: string | null;
  readonly cvDocumentId: string | null;
  readonly catalogVersion: string | null;
  readonly jobTitle: string | null;
  readonly companyName: string | null;
  readonly hasCvSnapshot: boolean;
  readonly hasJobSnapshot: boolean;
  readonly failureReason: string | null;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly preparedAt: string | null;
  readonly startedAt: string | null;
  readonly completedAt: string | null;
  readonly cancelledAt: string | null;
  readonly eTag: string;
  readonly brief: InterviewPrepBrief | null;
  readonly plan: InterviewPrepPlan | null;
  readonly stages: readonly InterviewPrepStage[];
  readonly turns: readonly InterviewPrepTurn[];
}

export interface InterviewPrepTurnSubmitResponse {
  readonly session: InterviewPrepSessionDetail;
  readonly candidateTurn: InterviewPrepTurn;
  readonly nextInterviewerTurn: InterviewPrepTurn | null;
  readonly interviewComplete: boolean;
  /**
   * True when this answer triggered a Full-loop mid-stage Stage handoff + next Stage open
   * in the same response. Mid-loop auto-advance keeps interviewComplete false.
   */
  readonly stageTransitionOccurred?: boolean;
}

export interface InterviewPrepTranscriptTurn {
  readonly id: string;
  readonly sequence: number;
  readonly role: InterviewPrepTurnRole | string;
  readonly text: string;
  readonly createdAt: string;
}

export interface InterviewPrepTranscript {
  readonly sessionId: string;
  readonly status: string;
  readonly turns: readonly InterviewPrepTranscriptTurn[];
}

export interface InterviewPrepMissingEvidence {
  readonly signal: string;
  readonly reason: string;
  readonly isUnknownNotWeakness: boolean;
}

export interface InterviewPrepJobRequirementCoverage {
  readonly competencyId: string;
  readonly displayName: string;
  readonly coverageState: string;
  readonly jobAlignmentNote: string | null;
  readonly confidence: string;
}

export interface InterviewPrepJobCoverageSummary {
  readonly jobTitle: string | null;
  readonly companyName: string | null;
  readonly requirements: readonly InterviewPrepJobRequirementCoverage[];
}

export interface InterviewPrepAnswerQualityPattern {
  readonly pattern: string;
  readonly detail: string;
  readonly confidence: string;
}

export interface InterviewPrepEvidenceTrace {
  readonly competencyId: string;
  readonly claim: string;
  readonly evidenceQuote: string;
  readonly candidateTurnSequence: number | null;
  readonly classification: string;
}

export interface InterviewPrepStageSummary {
  readonly stageKey: string;
  readonly summary: string;
  readonly highlights: readonly string[];
  readonly missedGoals: readonly string[];
  readonly confidence: string;
}

export interface InterviewPrepCandidateReport {
  readonly sessionId: string;
  readonly status: string;
  readonly disclaimer: string;
  readonly summary: string;
  readonly strengths: readonly string[];
  readonly developmentAreas: readonly string[];
  readonly missingEvidence: readonly InterviewPrepMissingEvidence[];
  readonly jobCoverage: InterviewPrepJobCoverageSummary | null;
  readonly answerQualityPatterns: readonly InterviewPrepAnswerQualityPattern[];
  readonly practiceRecommendations: readonly string[];
  readonly overallConfidence: string;
  readonly evidenceTrace: readonly InterviewPrepEvidenceTrace[];
  readonly stageSummaries: readonly InterviewPrepStageSummary[];
  readonly languageFeedback: readonly string[] | null;
  readonly generatedAt: string;
  readonly usedAiFallback: boolean;
}

export interface InterviewPrepCompetencyEvidence {
  readonly claim: string;
  readonly evidenceQuote: string;
  readonly classification: string;
  readonly strength: string;
  readonly confidence: string;
}

export interface InterviewPrepCompetencyResult {
  readonly competencyId: string;
  readonly displayName: string;
  readonly coverageState: string;
  readonly evidenceCount: number;
  readonly attemptCount: number;
  readonly confidence: string;
  readonly supportingEvidence: readonly InterviewPrepCompetencyEvidence[];
  readonly observedGaps: readonly string[];
}

export interface InterviewPrepCompetencyResults {
  readonly sessionId: string;
  readonly disclaimer: string;
  readonly competencies: readonly InterviewPrepCompetencyResult[];
}

export interface InterviewPrepAnswerReview {
  readonly retryId: string;
  readonly candidateTurnId: string;
  readonly interviewerTurnId: string;
  readonly questionText: string;
  readonly originalAnswerText: string;
  readonly answerSummary: string;
  readonly strengths: readonly string[];
  readonly gaps: readonly string[];
  /** @deprecated ADR-0026 — free-text blurb; unused in UI. Prefer modelAnswer. */
  readonly coachingFeedback: string;
  readonly modelAnswer: string;
  readonly coachingTips: readonly string[];
  readonly practiceRecommendations: readonly string[];
  readonly status: string;
  readonly updatedAt: string;
}

export interface InterviewPrepAnswerRetryResult {
  readonly retryId: string;
  readonly candidateTurnId: string;
  readonly interviewerTurnId: string;
  readonly questionText: string;
  readonly originalAnswerText: string;
  readonly revisedAnswerText: string | null;
  readonly answerSummary: string;
  readonly strengths: readonly string[];
  readonly gaps: readonly string[];
  /** @deprecated ADR-0026 — free-text blurb; unused in UI. Prefer modelAnswer / comparisonSummary. */
  readonly coachingFeedback: string;
  readonly modelAnswer: string;
  readonly coachingTips: readonly string[];
  readonly practiceRecommendations: readonly string[];
  readonly comparisonSummary: string | null;
  readonly improved: boolean | null;
  readonly improvements: readonly string[];
  readonly remainingGaps: readonly string[];
  readonly status: string;
  readonly updatedAt: string;
}

export interface InterviewPrepPanelPerspective {
  readonly personaLabel: string;
  readonly assessment: string;
  readonly score: number;
}

export interface InterviewPrepPanelMissingEvidence {
  readonly signal: string;
  readonly reason: string;
}

export interface InterviewPrepPanelDebrief {
  readonly overallDebrief: string;
  readonly perspectives: readonly InterviewPrepPanelPerspective[];
  readonly evidenceHighlights: readonly string[];
  readonly contradictions: readonly string[];
  readonly missingEvidence: readonly InterviewPrepPanelMissingEvidence[];
  readonly overallConfidence: string;
  readonly source: string;
  readonly usedAiFallback: boolean;
  readonly generatedAt: string;
}

export interface InterviewPrepApiErrorBody {
  readonly message?: string;
  readonly code?: string;
  readonly existingBriefId?: string;
}

/** Durable study artifact (ADR-0025). Distinct from session prepare `InterviewPrepBrief`. */
export type InterviewPrepBriefTopicGap =
  | 'alreadyStrong'
  | 'mustStudy'
  | 'niceToHave'
  | 'unclear';

export type InterviewPrepBriefOutdatedReason = 'structuredCvChanged' | 'boundJobMissing';

export type InterviewPrepPageSurface = 'practice' | 'study';

export interface InterviewPrepGenerateStudyBriefRequest {
  readonly language: InterviewPrepLanguage;
  readonly market: InterviewPrepMarket;
  readonly scrapeResultId?: string | null;
  readonly focusNote?: string | null;
}

export interface InterviewPrepRegenerateStudyBriefRequest {
  readonly focusNote?: string | null;
  readonly language?: InterviewPrepLanguage | null;
  readonly market?: InterviewPrepMarket | null;
}

export interface InterviewPrepStudyBriefItem {
  readonly text: string;
  readonly note?: string | null;
}

export interface InterviewPrepStudyBriefTopic {
  readonly name: string;
  readonly gap: InterviewPrepBriefTopicGap | string;
  readonly priority: number;
  readonly note?: string | null;
  /** Coverage items (syllabus lines); ≥1 per topic. Not checklists. */
  readonly coverageItems: readonly InterviewPrepStudyBriefItem[];
  readonly sampleQuestions: readonly InterviewPrepStudyBriefItem[];
  readonly talkingPoints: readonly InterviewPrepStudyBriefItem[];
}

export interface InterviewPrepStudyBrief {
  readonly id: string;
  readonly scrapeResultId: string | null;
  readonly jobTitle: string | null;
  readonly companyName: string | null;
  readonly language: InterviewPrepLanguage | string;
  readonly market: InterviewPrepMarket | string;
  readonly focusNoteSnapshot: string | null;
  readonly outdated: boolean;
  readonly outdatedReasons: readonly (InterviewPrepBriefOutdatedReason | string)[];
  readonly generatedAt: string;
  readonly updatedAt: string;
  readonly topics: readonly InterviewPrepStudyBriefTopic[];
  readonly usedAiFallback: boolean;
}

export interface InterviewPrepStudyBriefListResponse {
  readonly items: readonly InterviewPrepStudyBrief[];
}

export interface InterviewPrepStudyBriefListQuery {
  readonly scrapeResultId?: string;
  readonly cvOnly?: boolean;
}

export const INTERVIEW_PREP_BRIEF_TOPIC_GAPS: ReadonlyArray<{
  readonly id: InterviewPrepBriefTopicGap;
  readonly label: string;
}> = [
  { id: 'alreadyStrong', label: 'Already strong' },
  { id: 'mustStudy', label: 'Must study' },
  { id: 'niceToHave', label: 'Nice to have' },
  { id: 'unclear', label: 'Unclear' }
];

export const INTERVIEW_PREP_FOCUS_NOTE_MAX_LENGTH = 2000;

/** Mirrors backend InterviewPrepOperationalCatalog mode×persona pairs. */
export const INTERVIEW_PREP_MODE_PERSONA_PAIRS: ReadonlyArray<{
  readonly mode: InterviewPrepMode;
  readonly persona: InterviewPrepPersona;
}> = [
  { mode: 'screeningAndMotivation', persona: 'recruiter' },
  { mode: 'screeningAndMotivation', persona: 'hiringManager' },
  { mode: 'screeningAndMotivation', persona: 'seniorPeer' },
  { mode: 'behavioralAndCulture', persona: 'recruiter' },
  { mode: 'behavioralAndCulture', persona: 'hiringManager' },
  { mode: 'behavioralAndCulture', persona: 'seniorPeer' },
  { mode: 'behavioralAndCulture', persona: 'barRaiser' },
  { mode: 'roleAndDomainDepth', persona: 'recruiter' },
  { mode: 'roleAndDomainDepth', persona: 'hiringManager' },
  { mode: 'roleAndDomainDepth', persona: 'seniorPeer' },
  { mode: 'roleAndDomainDepth', persona: 'barRaiser' },
  { mode: 'processAndSystems', persona: 'recruiter' },
  { mode: 'processAndSystems', persona: 'hiringManager' },
  { mode: 'processAndSystems', persona: 'seniorPeer' },
  { mode: 'processAndSystems', persona: 'barRaiser' },
  { mode: 'problemSolvingCase', persona: 'hiringManager' },
  { mode: 'problemSolvingCase', persona: 'seniorPeer' },
  { mode: 'problemSolvingCase', persona: 'barRaiser' },
  { mode: 'languagePractice', persona: 'recruiter' },
  { mode: 'languagePractice', persona: 'hiringManager' },
  { mode: 'languagePractice', persona: 'seniorPeer' },
  { mode: 'fullLoop', persona: 'hiringManager' }
];

export const INTERVIEW_PREP_MODES: readonly InterviewPrepModeOption[] = [
  {
    id: 'screeningAndMotivation',
    label: 'Screening & motivation',
    description: 'Fit, motivation, and role interest.'
  },
  {
    id: 'behavioralAndCulture',
    label: 'Behavioral & culture',
    description: 'Past behavior, collaboration, and values.'
  },
  {
    id: 'roleAndDomainDepth',
    label: 'Role & domain depth',
    description: 'Technical and role-specific depth.'
  },
  {
    id: 'processAndSystems',
    label: 'Process & systems',
    description: 'Delivery, quality, and systems thinking.'
  },
  {
    id: 'problemSolvingCase',
    label: 'Problem-solving case',
    description: 'Structured case or problem walkthrough.'
  },
  {
    id: 'languagePractice',
    label: 'Language practice',
    description: 'Professional language practice for interviews.'
  },
  {
    id: 'fullLoop',
    label: 'Full loop',
    description: 'Multi-stage loop with panel debrief.'
  }
];

export const INTERVIEW_PREP_PERSONAS: readonly InterviewPrepPersonaOption[] = [
  { id: 'recruiter', label: 'Recruiter' },
  { id: 'hiringManager', label: 'Hiring manager' },
  { id: 'seniorPeer', label: 'Senior peer' },
  { id: 'barRaiser', label: 'Bar raiser' }
];

export const INTERVIEW_PREP_LANGUAGES: readonly InterviewPrepLanguageOption[] = [
  { id: 'english', label: 'English' },
  { id: 'danish', label: 'Danish' },
  { id: 'mixedEnglishDanish', label: 'Mixed EN / DA' }
];

export const INTERVIEW_PREP_MARKETS: readonly InterviewPrepMarketOption[] = [
  { id: 'general', label: 'General' },
  { id: 'danish', label: 'Danish market' }
];

export const INTERVIEW_PREP_EXPERIENCE_TYPES: readonly InterviewPrepExperienceTypeOption[] = [
  {
    id: 'realisticSimulation',
    label: 'Realistic simulation',
    description: 'Interview first; coaching after completion.'
  },
  {
    id: 'guidedCoaching',
    label: 'Guided coaching',
    description: 'Review and retry answers during practice.'
  }
];

export const DEFAULT_INTERVIEW_PREP_MODE: InterviewPrepMode = 'behavioralAndCulture';
export const DEFAULT_INTERVIEW_PREP_PERSONA: InterviewPrepPersona = 'hiringManager';
export const DEFAULT_INTERVIEW_PREP_LANGUAGE: InterviewPrepLanguage = 'english';
export const DEFAULT_INTERVIEW_PREP_MARKET: InterviewPrepMarket = 'general';
export const DEFAULT_INTERVIEW_PREP_EXPERIENCE_TYPE: InterviewPrepExperienceType =
  'guidedCoaching';

export function personasForMode(mode: InterviewPrepMode): readonly InterviewPrepPersona[] {
  const personas = INTERVIEW_PREP_MODE_PERSONA_PAIRS
    .filter((pair) => pair.mode === mode)
    .map((pair) => pair.persona);
  return personas;
}

export function isPersonaValidForMode(
  mode: InterviewPrepMode,
  persona: InterviewPrepPersona
): boolean {
  return personasForMode(mode).includes(persona);
}

export function defaultPersonaForMode(mode: InterviewPrepMode): InterviewPrepPersona {
  const personas = personasForMode(mode);
  return personas[0] ?? DEFAULT_INTERVIEW_PREP_PERSONA;
}
