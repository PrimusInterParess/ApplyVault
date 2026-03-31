import { HiringManagerContact } from './job-result.model';

export interface JobResultViewModel {
  readonly id: string;
  readonly savedAt: string;
  readonly extractedAt: string;
  readonly title: string;
  readonly company: string;
  readonly location: string;
  readonly sourceHostname: string;
  readonly detectedPageType: string;
  readonly summary: string;
  readonly description: string;
  readonly excerpt: string;
  readonly hiringManagerName: string;
  readonly hiringManagerContacts: readonly HiringManagerContact[];
  readonly url: string;
  readonly textLength: number;
  readonly searchText: string;
}

export interface JobResultsStats {
  readonly totalResults: number;
  readonly companies: number;
  readonly sources: number;
  readonly remoteFriendly: number;
}
