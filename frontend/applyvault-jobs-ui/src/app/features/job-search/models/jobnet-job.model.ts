export type JobnetDescriptionSource = 'nativeDetail' | 'searchFallback';
export type JobnetDescriptionQuality = 'full' | 'previewOnly';

export interface JobnetJobSearchRequest {
  keywords: readonly string[];
  page?: number;
  resultsPerPage?: number;
  requestLanguage?: string;
}

export interface JobnetJobListing {
  id: string;
  title: string | null;
  employer: string | null;
  location: string | null;
  publicationDate: string | null;
  sourceUrl: string | null;
  workInDenmark: boolean;
}

export interface JobnetJobSearchResponse {
  totalResults: number;
  page: number;
  resultsPerPage: number;
  jobs: readonly JobnetJobListing[];
  upstreamTotalResults?: number | null;
  resultsTruncated?: boolean;
}

export interface JobnetJobDetail {
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
  workInDenmark: boolean;
  descriptionSource: JobnetDescriptionSource;
  descriptionQuality: JobnetDescriptionQuality;
  descriptionExcerpt: string | null;
  descriptionQualityReason: string | null;
}

export interface SaveJobnetJobResponse {
  id: string;
  savedAt: string;
  alreadyExists: boolean;
}
