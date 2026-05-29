import { ParamMap } from '@angular/router';
import { Signal } from '@angular/core';

import { JobSearchSource } from './job-source.model';

export interface JobSearchProviderFacade {
  readonly keywords: Signal<readonly string[]>;
  readonly loading: Signal<boolean>;
  readonly loadingMore: Signal<boolean>;
  readonly loadMoreError: Signal<string | null>;
  readonly detailLoading: Signal<boolean>;
  readonly error: Signal<string | null>;
  readonly detailError: Signal<string | null>;
  readonly totalResults: Signal<number>;
  readonly selectedJobId: Signal<string | null>;
  readonly hasSearched: Signal<boolean>;
  readonly saving: Signal<boolean>;
  readonly saveError: Signal<string | null>;
  readonly savedJobId: Signal<string | null>;
  readonly saveAlreadyExists: Signal<boolean>;
  readonly searchGeneration: Signal<number>;
  readonly lastSearchedAt: Signal<Date | null>;
  readonly keywordsLabel: Signal<string>;
  readonly hasActiveSearch: Signal<boolean>;
  readonly initialLoading: Signal<boolean>;
  readonly resultsSummary: Signal<string>;
  readonly hasMoreResults: Signal<boolean>;
  readonly hasValidLocation: Signal<boolean>;

  initFromQueryParams(params: ParamMap): void;
  loadInitialSearch(selectJobId?: string | null): void;
  restoreFromUrlState(selectJobId?: string | null): void;
  search(draftKeywords?: string): void;
  refreshCurrentSearch(): void;
  loadMore(): void;
  isListingSaved(id: string): boolean;
  selectWhenLoaded(id: string): void;
  toggleKeyword(keyword: string): void;
  addKeywords(values: readonly string[]): void;
  removeKeyword(keyword: string): void;
  clearKeywords(): void;
  isKeywordSelected(keyword: string): boolean;
  select(id: string): void;
  clearSelection(): void;
  saveSelectedJob(): void;
}

export type JobSearchProviderRegistry = Readonly<Record<JobSearchSource, JobSearchProviderFacade>>;
