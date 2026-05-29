export type JobSearchSource = 'eures';

export const JOB_SEARCH_SOURCES = ['eures'] as const satisfies readonly JobSearchSource[];

export interface JobSearchProviderDefinition {
  readonly id: JobSearchSource;
  readonly label: string;
  readonly searchActionLabel: string;
  readonly monogram: string;
  readonly detailLabel: string;
}

export const JOB_SEARCH_PROVIDERS: readonly JobSearchProviderDefinition[] = [
  {
    id: 'eures',
    label: 'EURES',
    searchActionLabel: 'Search EURES',
    monogram: 'EU',
    detailLabel: 'EURES listing'
  }
];

export function normalizeJobSearchSource(value: string | null | undefined): JobSearchSource {
  const normalized = value?.trim().toLowerCase();
  return JOB_SEARCH_SOURCES.find((source) => source === normalized) ?? 'eures';
}

export function getJobSearchProvider(source: JobSearchSource): JobSearchProviderDefinition {
  return JOB_SEARCH_PROVIDERS.find((provider) => provider.id === source) ?? JOB_SEARCH_PROVIDERS[0];
}

export function hasMultipleJobSearchProviders(): boolean {
  return JOB_SEARCH_PROVIDERS.length > 1;
}
