import { JobSearchSource } from './job-source.model';

export interface ExternalJobListing {
  id: string;
  title: string | null;
  employer: string | null;
  location: string | null;
  publicationDate: string | null;
  sourceUrl: string | null;
  remote?: boolean;
}

export interface ExternalJobDetail extends ExternalJobListing {
  description: string | null;
  applicationUrl: string | null;
  contractType?: string | null;
  workHours?: string | null;
  tags?: readonly string[];
  jobTypes?: readonly string[];
  descriptionSource?: 'nativeDetail' | 'searchFallback';
  descriptionQuality?: 'full' | 'previewOnly';
  descriptionExcerpt?: string | null;
  descriptionQualityReason?: string | null;
}

export interface JobSearchUrlQueryParams {
  source: JobSearchSource | null;
  keywords: string | null;
  country: string | null;
  location: string | null;
  selected: string | null;
}
