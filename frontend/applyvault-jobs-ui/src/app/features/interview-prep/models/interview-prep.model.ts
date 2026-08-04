/** Interview Prep coach DTOs — ADR-0012 / frozen contracts. */

export type InterviewPrepMode =
  | 'screening'
  | 'behavioral'
  | 'role_domain'
  | 'problem_solving'
  | 'process_systems'
  | 'language_practice'
  | 'full_loop';

export type InterviewPrepLanguageMix = 'en' | 'da' | 'mixed';

export type InterviewPrepHiringMarket = 'general' | 'dk';

export type InterviewPrepPhase = 'interview' | 'debrief';

export type InterviewPrepTurnRole = 'user' | 'coach';

export type InterviewPrepSessionStatus = 'in_progress' | 'completed';

export type InterviewPrepInterviewerProfile =
  | 'recruiter'
  | 'hiring_manager'
  | 'senior_peer'
  | 'bar_raiser';

export type InterviewPrepInterviewMove =
  | 'ask_new_question'
  | 'probe_evidence'
  | 'clarify_ambiguity'
  | 'challenge_claim'
  | 'transition_topic'
  | 'close_round';

export type InterviewPrepPressureLevel = 'low' | 'medium' | 'high';

export type InterviewPrepScorecardDimensionId =
  | 'clarity'
  | 'evidence'
  | 'structure'
  | 'role_fit'
  | 'language';

export interface InterviewPrepModeOption {
  readonly id: InterviewPrepMode;
  readonly label: string;
  /** Short line for chip title / quick scan. */
  readonly description: string;
  /** What this round feels like and what you practice. */
  readonly detail: string;
  /** Concrete example questions the coach may ask. */
  readonly example: string;
}

export interface InterviewPrepLanguageOption {
  readonly id: InterviewPrepLanguageMix;
  readonly label: string;
  readonly description: string;
  readonly detail: string;
}

export interface InterviewPrepHiringMarketOption {
  readonly id: InterviewPrepHiringMarket;
  readonly label: string;
  readonly description: string;
  readonly detail: string;
}

export interface InterviewPrepInterviewerProfileOption {
  readonly id: InterviewPrepInterviewerProfile;
  readonly label: string;
  readonly description: string;
  readonly detail: string;
}

export interface InterviewPrepPriorTurn {
  readonly role: InterviewPrepTurnRole;
  readonly text: string;
  readonly phase?: InterviewPrepPhase;
}

export interface InterviewPrepTurnRequest {
  readonly mode: InterviewPrepMode;
  readonly userMessage: string;
  readonly languageMix?: InterviewPrepLanguageMix;
  readonly hiringMarket?: InterviewPrepHiringMarket;
  readonly scrapeResultId?: string | null;
  readonly priorTurns?: readonly InterviewPrepPriorTurn[];
  /** When set, durable turn path (ADR-0016). */
  readonly sessionId?: string | null;
}

export interface InterviewPrepCreateSessionRequest {
  readonly mode: InterviewPrepMode;
  readonly languageMix?: InterviewPrepLanguageMix;
  readonly hiringMarket?: InterviewPrepHiringMarket;
  readonly scrapeResultId?: string | null;
  readonly interviewerProfile?: InterviewPrepInterviewerProfile | null;
}

export interface InterviewPrepSessionSummary {
  readonly id: string;
  readonly mode: InterviewPrepMode | string;
  readonly languageMix: InterviewPrepLanguageMix | string;
  readonly hiringMarket: InterviewPrepHiringMarket | string;
  readonly interviewerProfile: InterviewPrepInterviewerProfile | string;
  readonly currentAgendaStep: string;
  readonly latestInterviewMove: InterviewPrepInterviewMove | string | null;
  readonly scrapeResultId: string | null;
  readonly jobTitle: string | null;
  readonly companyName: string | null;
  readonly status: InterviewPrepSessionStatus | string;
  readonly phase: InterviewPrepPhase | string;
  readonly latestOverallScore: number | null;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly completedAt: string | null;
}

export interface InterviewPrepSessionListResponse {
  readonly items: readonly InterviewPrepSessionSummary[];
  readonly totalCount: number;
}

export interface InterviewPrepSessionMessage {
  readonly id: string;
  readonly sequence: number;
  readonly role: InterviewPrepTurnRole | string;
  readonly text: string;
  readonly phase: InterviewPrepPhase | string;
  readonly scorecard: InterviewPrepScorecard | null;
  readonly followUps: readonly string[];
  readonly debriefBullets: readonly string[];
  readonly modelAnswer: string | null;
  readonly inference: InterviewPrepInference | null;
  readonly turnState: InterviewPrepTurnState | null;
  readonly createdAt: string;
}

export interface InterviewPrepSessionDetail extends InterviewPrepSessionSummary {
  readonly messages: readonly InterviewPrepSessionMessage[];
}

export interface InterviewPrepInference {
  readonly role: string;
  readonly seniority: string;
  readonly interviewStyle: string;
  readonly isTechnicalContext: boolean;
}

export interface InterviewPrepScorecardDimension {
  readonly id: InterviewPrepScorecardDimensionId | string;
  readonly score: number;
  readonly note: string;
}

export interface InterviewPrepScorecard {
  readonly overall: number;
  readonly summary: string | null;
  readonly dimensions: readonly InterviewPrepScorecardDimension[];
}

export interface InterviewPrepTurnResponse {
  readonly phase: InterviewPrepPhase | string;
  readonly inference: InterviewPrepInference;
  readonly coachMessage: string;
  readonly scorecard: InterviewPrepScorecard | null;
  readonly followUps: readonly string[];
  readonly debriefBullets: readonly string[];
  /** Grounded answer guide for the current coach question; null when unused / debrief. */
  readonly modelAnswer: string | null;
  /** Echoed when the turn was durable (ADR-0016). */
  readonly sessionId?: string | null;
  readonly turnState?: InterviewPrepTurnState | null;
}

export interface InterviewPrepTurnState {
  readonly interviewMove: InterviewPrepInterviewMove | string;
  readonly questionType: string;
  readonly pressureLevel: InterviewPrepPressureLevel | string;
  readonly interviewerIntent: string;
  readonly agendaStep: string;
  readonly nextAgendaStep: string | null;
  readonly memorySummary: string | null;
  readonly listeningNotes: readonly string[];
}

/** Display transcript row (hydrated from durable session or live turns). */
export interface InterviewPrepChatMessage {
  readonly id: string;
  readonly role: InterviewPrepTurnRole;
  readonly text: string;
  readonly phase: InterviewPrepPhase | string;
}

export const INTERVIEW_PREP_MODES: readonly InterviewPrepModeOption[] = [
  {
    id: 'screening',
    label: 'Screening / motivation',
    description: 'Best for the first recruiter or HR call.',
    detail:
      'This round checks whether you are a fit before deeper interviews. Practice a short career pitch, why you want this role or company, and what motivates you. Keep answers clear and confident — not overly technical yet.',
    example: '“Tell me about yourself.” · “Why this role?” · “What are you looking for next?”'
  },
  {
    id: 'behavioral',
    label: 'Behavioral / culture',
    description: 'Best when they ask about real past situations.',
    detail:
      'Interviewers want proof of how you work with people. Practice stories about teamwork, conflict, feedback, mistakes, and pressure. Use a clear structure: situation, what you did, and the result — with concrete details from your experience.',
    example: '“Tell me about a conflict at work.” · “Describe a time you failed.” · “How do you handle tight deadlines?”'
  },
  {
    id: 'role_domain',
    label: 'Role & domain depth',
    description: 'Best when they dig into your actual job skills.',
    detail:
      'This is the “can you do the work?” round. The coach asks deeper questions about your craft and domain, based on your Structured CV and optional saved job. Explain methods, tools, judgment calls, and examples from real work in your profession — not a generic script.',
    example: '“Walk me through a typical project in your role.” · “How do you decide X?” · “What tools or methods do you rely on?”'
  },
  {
    id: 'problem_solving',
    label: 'Problem-solving / case',
    description: 'Best for case, scenario, or “how would you…” interviews.',
    detail:
      'Here the goal is how you think, not a memorized answer. The coach gives a realistic problem. Practice clarifying the goal, structuring your approach, comparing options, and explaining trade-offs out loud — even if you do not know every detail.',
    example: '“A customer is unhappy — what do you do?” · “How would you prioritize this workload?” · “Walk me through your approach to this case.”'
  },
  {
    id: 'process_systems',
    label: 'Process & systems',
    description: 'Best when they ask how work flows end-to-end.',
    detail:
      'Practice explaining how work moves from start to finish: steps, handoffs, tools, quality checks, and what breaks when something goes wrong. If your background is technical, this may feel closer to system design; otherwise it stays about operations and process.',
    example: '“How does a request move through your team?” · “Where do delays usually happen?” · “How would you improve this process?”'
  },
  {
    id: 'language_practice',
    label: 'Language practice (EN / DA)',
    description: 'Best when English, Danish, or switching between them is the hard part.',
    detail:
      'Use this when you already know the job content, but want cleaner interview phrasing — including bilingual EN↔DA practice common in Danish hiring. The coach focuses on fluency, clarity, and natural answers in the language mix you pick below — with less pressure on deep domain expertise.',
    example: 'Shorter interview answers in English, Danish, or mixed — with feedback on clarity and phrasing.'
  },
  {
    id: 'full_loop',
    label: 'Full loop',
    description: 'Best for a longer dress rehearsal before a real interview day.',
    detail:
      'One practice session that moves through several round types — usually screening, then behavioral, then role-focused questions. Use this when you want stamina and flow, not just one skill. Stay in the chat; do not reset mid-session unless you want a fresh start.',
    example: 'A multi-part mock: intro → story questions → deeper role questions in one go.'
  }
] as const;

export const INTERVIEW_PREP_LANGUAGE_MIXES: readonly InterviewPrepLanguageOption[] = [
  {
    id: 'en',
    label: 'English',
    description: 'Questions and feedback stay in English.',
    detail:
      'Choose this for English-only interviews. Answer in English; the coach replies and scores language in English. For English interviews in Denmark, keep English here and set Hiring market → Danish market.'
  },
  {
    id: 'da',
    label: 'Danish',
    description: 'Questions and feedback stay in Danish.',
    detail: 'Choose this for Danish-only interviews. Answer in Danish; the coach replies and scores language in Danish.'
  },
  {
    id: 'mixed',
    label: 'Mixed (EN + DA)',
    description: 'The coach mixes English and Danish — common in Danish interviews.',
    detail:
      'Useful when Danish interviews switch between English and Danish. Practice answering comfortably in both.'
  }
] as const;

export const INTERVIEW_PREP_HIRING_MARKETS: readonly InterviewPrepHiringMarketOption[] = [
  {
    id: 'general',
    label: 'General',
    description: 'Market-agnostic coaching.',
    detail:
      'No forced country hiring norms. Use when you want neutral practice, or when market cues should come only from a clearly Denmark-linked saved job.'
  },
  {
    id: 'dk',
    label: 'Danish market',
    description: 'Coach for common Danish hiring norms.',
    detail:
      'Adds Danish-market coaching cues (motivation, culture, process). Spoken language stays whatever you pick under Language mix — English + Danish market is supported.'
  }
] as const;

export const INTERVIEW_PREP_INTERVIEWER_PROFILES: readonly InterviewPrepInterviewerProfileOption[] = [
  {
    id: 'recruiter',
    label: 'Recruiter',
    description: 'Motivation, fit, clarity, and practical expectations.',
    detail:
      'Feels like an early screening conversation: concise career pitch, why this role, communication, and general fit.'
  },
  {
    id: 'hiring_manager',
    label: 'Hiring manager',
    description: 'Role depth, judgment, ownership, and team fit.',
    detail:
      'Feels like the person responsible for the role is testing whether your experience maps to the work.'
  },
  {
    id: 'senior_peer',
    label: 'Senior peer',
    description: 'Practical craft, trade-offs, handoffs, and collaboration.',
    detail:
      'Feels like a future colleague asking how you actually solve problems and work with others.'
  },
  {
    id: 'bar_raiser',
    label: 'Bar raiser',
    description: 'Sharper probes and more challenge on vague claims.',
    detail:
      'Feels more demanding: the interviewer asks for evidence, trade-offs, and clearer examples before moving on.'
  }
] as const;

export const INTERVIEW_PREP_SCORECARD_DIMENSION_LABELS: Readonly<
  Record<InterviewPrepScorecardDimensionId, string>
> = {
  clarity: 'Clarity',
  evidence: 'Evidence',
  structure: 'Structure',
  role_fit: 'Role fit',
  language: 'Language'
};

export const DEFAULT_INTERVIEW_PREP_MODE: InterviewPrepMode = 'behavioral';
export const DEFAULT_INTERVIEW_PREP_LANGUAGE_MIX: InterviewPrepLanguageMix = 'en';
export const DEFAULT_INTERVIEW_PREP_HIRING_MARKET: InterviewPrepHiringMarket = 'general';
export const DEFAULT_INTERVIEW_PREP_INTERVIEWER_PROFILE: InterviewPrepInterviewerProfile = 'hiring_manager';
export const INTERVIEW_PREP_START_MESSAGE = "Let's start.";
