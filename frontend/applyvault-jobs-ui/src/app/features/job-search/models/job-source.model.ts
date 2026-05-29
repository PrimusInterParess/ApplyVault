export type JobSearchSource = 'eures' | 'jobnet';

export const JOB_SEARCH_SOURCES = ['eures', 'jobnet'] as const satisfies readonly JobSearchSource[];

export interface JobSearchProviderDefinition {
  readonly id: JobSearchSource;
  readonly label: string;
  readonly searchActionLabel: string;
  readonly monogram: string;
  readonly detailLabel: string;
  readonly idleSearchPrompt: string;
  readonly searchingPrompt: string;
  readonly emptyStateIntro: string;
}

export const JOB_SEARCH_PROVIDERS: readonly JobSearchProviderDefinition[] = [
  {
    id: 'eures',
    label: 'EURES',
    searchActionLabel: 'Search EURES',
    monogram: 'EU',
    detailLabel: 'EURES listing',
    idleSearchPrompt: 'Run a search to load EURES listings',
    searchingPrompt: 'Searching EURES listings...',
    emptyStateIntro: 'Run a search to load EURES listings, or try a popular IT search below.'
  },
  {
    id: 'jobnet',
    label: 'Work in Denmark',
    searchActionLabel: 'Search Work in Denmark',
    monogram: 'DK',
    detailLabel: 'Work in Denmark listing',
    idleSearchPrompt: 'Run a search to load Work in Denmark listings',
    searchingPrompt: 'Searching Work in Denmark listings...',
    emptyStateIntro: 'Run a search to load Work in Denmark listings, or try a popular IT search below.'
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
