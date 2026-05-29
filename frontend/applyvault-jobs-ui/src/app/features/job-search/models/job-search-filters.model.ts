import { JobSearchSource } from './job-source.model';

export function filterKeywordSuggestionGroupsForSource<
  T extends { readonly label: string }
>(groups: readonly T[], _source: JobSearchSource): readonly T[] {
  return groups;
}
