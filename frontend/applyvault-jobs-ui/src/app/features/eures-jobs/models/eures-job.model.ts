export interface EuresJobSearchRequest {
  keywords: readonly string[];
  locationCode?: string | null;
  page?: number;
  resultsPerPage?: number;
  requestLanguage?: string;
  sortSearch?: string;
}

export interface EuresJobListing {
  id: string;
  title: string | null;
  employer: string | null;
  location: string | null;
  publicationDate: string | null;
  sourceUrl: string | null;
}

export interface EuresJobSearchResponse {
  totalResults: number;
  page: number;
  resultsPerPage: number;
  jobs: readonly EuresJobListing[];
}

export interface EuresJobDetail {
  id: string;
  title: string | null;
  employer: string | null;
  location: string | null;
  publicationDate: string | null;
  sourceUrl: string | null;
  description: string | null;
  applicationUrl: string | null;
  contractType: string | null;
  workHours: string | null;
}

export interface SaveEuresJobResponse {
  id: string;
  savedAt: string;
  alreadyExists: boolean;
}
