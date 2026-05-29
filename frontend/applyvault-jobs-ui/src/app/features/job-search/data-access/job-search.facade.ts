import { computed, inject, Injectable, signal } from '@angular/core';
import { ParamMap } from '@angular/router';

import { ExternalJobDetail, ExternalJobListing } from '../models/external-job.model';
import { JobSearchProviderFacade, JobSearchProviderRegistry } from '../models/job-search-provider.model';
import {
  getJobSearchProvider,
  JobSearchSource,
  normalizeJobSearchSource
} from '../models/job-source.model';
import { EURES_LOCATION_OPTIONS } from '../models/eures-location-options';
import { buildJobSearchUrlQueryParams, normalizeJobSearchSourceFromParams } from '../utils/job-search-url-state.utils';
import { EuresJobsFacade } from './eures-jobs.facade';
import { JobnetJobsFacade } from './jobnet-jobs.facade';

@Injectable()
export class JobSearchFacade {
  private readonly euresFacade = inject(EuresJobsFacade);
  private readonly jobnetFacade = inject(JobnetJobsFacade);
  private readonly providerFacades: JobSearchProviderRegistry = {
    eures: this.euresFacade,
    jobnet: this.jobnetFacade
  };

  readonly source = signal<JobSearchSource>('eures');

  readonly keywords = computed(() => this.activeFacade().keywords());
  readonly loading = computed(() => this.activeFacade().loading());
  readonly loadingMore = computed(() => this.activeFacade().loadingMore());
  readonly loadMoreError = computed(() => this.activeFacade().loadMoreError());
  readonly detailLoading = computed(() => this.activeFacade().detailLoading());
  readonly error = computed(() => this.activeFacade().error());
  readonly detailError = computed(() => this.activeFacade().detailError());
  readonly totalResults = computed(() => this.activeFacade().totalResults());
  readonly selectedJobId = computed(() => this.activeFacade().selectedJobId());
  readonly hasSearched = computed(() => {
    const source = this.source();
    return this.providerFacades[source].hasSearched();
  });
  readonly saving = computed(() => this.activeFacade().saving());
  readonly saveError = computed(() => this.activeFacade().saveError());
  readonly savedJobId = computed(() => this.activeFacade().savedJobId());
  readonly saveAlreadyExists = computed(() => this.activeFacade().saveAlreadyExists());
  readonly searchGeneration = computed(() => this.activeFacade().searchGeneration());
  readonly lastSearchedAt = computed(() => this.activeFacade().lastSearchedAt());
  readonly keywordsLabel = computed(() => this.activeFacade().keywordsLabel());
  readonly hasActiveSearch = computed(() => this.activeFacade().hasActiveSearch());
  readonly initialLoading = computed(() => this.activeFacade().initialLoading());
  readonly resultsSummary = computed(() => {
    const source = this.source();
    return this.providerFacades[source].resultsSummary();
  });
  readonly idleSearchPrompt = computed(() => getJobSearchProvider(this.source()).idleSearchPrompt);
  readonly emptyStateIntro = computed(() => getJobSearchProvider(this.source()).emptyStateIntro);
  readonly hasMoreResults = computed(() => this.activeFacade().hasMoreResults());
  readonly hasValidLocation = computed(() => this.activeFacade().hasValidLocation());
  readonly locationInitWarning = computed(() =>
    this.source() === 'eures' ? this.euresFacade.locationInitWarning() : null
  );
  readonly locationCode = computed(() =>
    this.source() === 'eures' ? this.euresFacade.locationCode() : null
  );

  readonly results = computed((): readonly ExternalJobListing[] => {
    if (this.source() === 'eures') {
      return this.euresFacade.results().map((job) => ({
        id: job.id,
        title: job.title,
        employer: job.employer,
        location: job.location,
        publicationDate: job.publicationDate,
        sourceUrl: job.sourceUrl
      }));
    }

    if (this.source() === 'jobnet') {
      return this.jobnetFacade.results().map((job) => ({
        id: job.id,
        title: job.title,
        employer: job.employer,
        location: job.location,
        publicationDate: job.publicationDate,
        sourceUrl: job.sourceUrl
      }));
    }

    return [];
  });

  readonly selectedJob = computed((): ExternalJobDetail | null => {
    if (this.source() === 'eures') {
      const job = this.euresFacade.selectedJob();

      if (!job) {
        return null;
      }

      return {
        id: job.id,
        title: job.title,
        employer: job.employer,
        location: job.location,
        publicationDate: job.publicationDate,
        sourceUrl: job.sourceUrl,
        description: job.description,
        applicationUrl: job.applicationUrl,
        contractType: job.contractType,
        workHours: job.workHours
      };
    }

    if (this.source() === 'jobnet') {
      const job = this.jobnetFacade.selectedJob();

      if (!job) {
        return null;
      }

      return {
        id: job.id,
        title: job.title,
        employer: job.employer,
        location: job.location,
        publicationDate: job.publicationDate,
        sourceUrl: job.sourceUrl,
        description: job.description,
        applicationUrl: job.applicationUrl,
        contractType: job.contractType,
        workHours: job.workHours
      };
    }

    return null;
  });

  setSource(nextSource: JobSearchSource): void {
    if (this.source() === nextSource || !this.providerFacades[nextSource]) {
      return;
    }

    this.syncSharedKeywords(this.activeFacade().keywords());
    this.source.set(nextSource);
  }

  initFromQueryParams(params: ParamMap): void {
    this.source.set(normalizeJobSearchSourceFromParams(params));
    this.euresFacade.initFromQueryParams(params);
    this.jobnetFacade.initFromQueryParams(params);
  }

  loadInitialSearch(selectJobId?: string | null): void {
    this.activeFacade().loadInitialSearch(selectJobId);
  }

  restoreFromUrlState(selectJobId?: string | null): void {
    this.activeFacade().restoreFromUrlState(selectJobId);
  }

  buildQueryParamState() {
    const source = this.source();

    return buildJobSearchUrlQueryParams({
      source,
      keywords: this.activeFacade().keywords(),
      country: source === 'eures' ? this.euresFacade.locationCode() : null,
      selectedJobId: this.activeFacade().selectedJobId()
    });
  }

  search(draftKeywords?: string): void {
    this.activeFacade().search(draftKeywords);
    this.syncSharedKeywords(this.activeFacade().keywords());
  }

  refreshCurrentSearch(): void {
    this.activeFacade().refreshCurrentSearch();
  }

  loadMore(): void {
    this.activeFacade().loadMore();
  }

  isListingSaved(id: string): boolean {
    return this.activeFacade().isListingSaved(id);
  }

  selectWhenLoaded(id: string): void {
    this.activeFacade().selectWhenLoaded(id);
  }

  toggleKeyword(keyword: string): void {
    this.activeFacade().toggleKeyword(keyword);
    this.syncSharedKeywords(this.activeFacade().keywords());
  }

  addKeywords(values: readonly string[]): void {
    this.activeFacade().addKeywords(values);
    this.syncSharedKeywords(this.activeFacade().keywords());
  }

  removeKeyword(keyword: string): void {
    this.activeFacade().removeKeyword(keyword);
    this.syncSharedKeywords(this.activeFacade().keywords());
  }

  clearKeywords(): void {
    this.activeFacade().clearKeywords();
    this.syncSharedKeywords(this.activeFacade().keywords());
  }

  isKeywordSelected(keyword: string): boolean {
    return this.activeFacade().isKeywordSelected(keyword);
  }

  select(id: string): void {
    this.activeFacade().select(id);
  }

  clearSelection(): void {
    this.activeFacade().clearSelection();
  }

  updateLocationCode(value: string): void {
    this.euresFacade.updateLocationCode(value);
  }

  saveSelectedJob(): void {
    this.activeFacade().saveSelectedJob();
  }

  locationLabel(code: string): string {
    return EURES_LOCATION_OPTIONS.find((option) => option.code === code)?.label ?? code;
  }

  providerLabel(source: JobSearchSource = this.source()): string {
    return getJobSearchProvider(source).label;
  }

  searchActionLabel(source: JobSearchSource = this.source()): string {
    return getJobSearchProvider(source).searchActionLabel;
  }

  private activeFacade(): JobSearchProviderFacade {
    return this.providerFacades[this.source()];
  }

  private syncSharedKeywords(keywords: readonly string[]): void {
    const sharedKeywords = [...keywords];
    this.euresFacade.keywords.set(sharedKeywords);
    this.jobnetFacade.keywords.set(sharedKeywords);
  }
}
