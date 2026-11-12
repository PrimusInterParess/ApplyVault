export interface CreateCoverLetterRequest {
  readonly note: string | null;
}

export interface CoverLetter {
  readonly scrapeResultId: string;
  readonly letter: string;
  readonly updatedAt: string;
}
