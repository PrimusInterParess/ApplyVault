import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { ParamMap } from '@angular/router';
import { Subscription } from 'rxjs';

import { resolveHttpErrorMessage } from '../../../core/http/api-error-message';
import { AuthService } from '../../../core/auth/auth.service';
import { JobResultsFacade } from '../../job-results/data-access/job-results.facade';
import {
  EuresJobDetail,
  EuresJobListing,
  EuresJobSearchRequest
} from '../models/eures-job.model';
import {
  EURES_DEFAULT_LOCATION_CODE,
  isKnownEuresLocationCode,
  normalizeEuresLocationCode
} from '../models/eures-location-options';
import {
  canonicalizeEuresKeyword,
  matchesEuresKeyword,
  normalizeEuresKeywords
} from '../utils/eures-keyword.utils';
import {
  buildEuresUrlQueryParams,
  EuresUrlQueryParams
} from '../utils/eures-url-state.utils';
import { EuresJobsApiService } from './eures-jobs-api.service';

export const EURES_DEFAULT_RESULTS_PER_PAGE = 15;

export const EURES_PAGE_SIZE_OPTIONS = [10, 15, 20] as const;

type FetchPageOptions = {
  resetSelection: boolean;
  autoSelectFirst: boolean;
  selectJobId?: string | null;
};

@Injectable()
export class EuresJobsFacade {
  private readonly authService = inject(AuthService);
  private readonly apiService = inject(EuresJobsApiService);
  private readonly jobResultsFacade = inject(JobResultsFacade);
  private searchSubscription: Subscription | null = null;
  private detailSubscription: Subscription | null = null;
  private saveSubscription: Subscription | null = null;

  readonly resultsPerPage = signal(EURES_DEFAULT_RESULTS_PER_PAGE);

  readonly keywords = signal<string[]>(['software']);
  readonly locationCode = signal(EURES_DEFAULT_LOCATION_CODE);
  readonly locationInitWarning = signal<string | null>(null);
  readonly requestLanguage = signal('en');
  readonly page = signal(1);
  readonly loading = signal(false);
  readonly detailLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly detailError = signal<string | null>(null);
  readonly totalResults = signal(0);
  readonly results = signal<readonly EuresJobListing[]>([]);
  readonly selectedJobId = signal<string | null>(null);
  readonly selectedJob = signal<EuresJobDetail | null>(null);
  readonly hasSearched = signal(false);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly savedJobId = signal<string | null>(null);
  readonly saveAlreadyExists = signal(false);
  readonly searchGeneration = signal(0);

  readonly keywordsLabel = computed(() => this.keywords().join(', '));

  readonly totalPages = computed(() => {
    const total = this.totalResults();

    if (total <= 0) {
      return 0;
    }

    return Math.ceil(total / this.resultsPerPage());
  });

  readonly pageRangeLabel = computed(() => {
    const total = this.totalResults();
    const currentPage = this.page();

    if (total <= 0) {
      return '';
    }

    const start = (currentPage - 1) * this.resultsPerPage() + 1;
    const end = Math.min(currentPage * this.resultsPerPage(), total);
    return `${start}-${end} of ${total}`;
  });

  readonly canGoToPreviousPage = computed(() => this.page() > 1 && !this.loading());
  readonly canGoToNextPage = computed(
    () => this.page() < this.totalPages() && !this.loading()
  );

  readonly hasValidLocation = computed(() => isKnownEuresLocationCode(this.locationCode()));

  constructor() {
    effect(
      () => {
        if (!this.authService.session()) {
          this.cancelPendingRequests();
          this.resetState();
        }
      },
      { allowSignalWrites: true }
    );
  }

  initFromQueryParams(params: ParamMap): void {
    const keywordsParam = params.get('keywords');

    if (keywordsParam?.trim()) {
      const parsedKeywords = normalizeEuresKeywords(keywordsParam.split(/[,;]+/));

      if (parsedKeywords.length > 0) {
        this.keywords.set(parsedKeywords);
      }
    }

    const locationParam = params.get('location');

    if (locationParam?.trim()) {
      const normalizedLocation = normalizeEuresLocationCode(locationParam);

      if (isKnownEuresLocationCode(normalizedLocation)) {
        this.locationCode.set(normalizedLocation);
        this.locationInitWarning.set(null);
      } else {
        this.locationCode.set(EURES_DEFAULT_LOCATION_CODE);
        this.locationInitWarning.set(
          `Unknown country code "${locationParam.trim()}". Using Denmark instead.`
        );
      }
    }

    const pageParam = params.get('page');

    if (pageParam) {
      const parsedPage = Number.parseInt(pageParam, 10);

      if (!Number.isNaN(parsedPage) && parsedPage >= 1) {
        this.page.set(parsedPage);
      }
    }

    const pageSizeParam = params.get('pageSize');

    if (pageSizeParam) {
      const parsedPageSize = Number.parseInt(pageSizeParam, 10);

      if (EURES_PAGE_SIZE_OPTIONS.includes(parsedPageSize as (typeof EURES_PAGE_SIZE_OPTIONS)[number])) {
        this.resultsPerPage.set(parsedPageSize);
      }
    }
  }

  loadInitialSearch(selectJobId?: string | null): void {
    if (!this.authService.session() || this.hasSearched()) {
      return;
    }

    if (!this.hasValidLocation()) {
      return;
    }

    this.fetchPage(this.page(), {
      resetSelection: !selectJobId,
      autoSelectFirst: !selectJobId,
      selectJobId: selectJobId ?? null
    });
  }

  restoreFromUrlState(selectJobId?: string | null): void {
    if (!this.authService.session() || !this.hasValidLocation()) {
      return;
    }

    this.fetchPage(this.page(), {
      resetSelection: !selectJobId,
      autoSelectFirst: !selectJobId,
      selectJobId: selectJobId ?? null
    });
  }

  buildQueryParamState(): EuresUrlQueryParams {
    return buildEuresUrlQueryParams({
      keywords: this.keywords(),
      locationCode: this.locationCode(),
      page: this.page(),
      selectedJobId: this.selectedJobId(),
      resultsPerPage: this.resultsPerPage(),
      defaultResultsPerPage: EURES_DEFAULT_RESULTS_PER_PAGE
    });
  }

  search(draftKeywords?: string): void {
    if (draftKeywords?.trim()) {
      this.addKeywords(draftKeywords.split(/[,;]+/));
    }

    this.page.set(1);
    this.fetchPage(1, { resetSelection: true, autoSelectFirst: true });
  }

  refreshCurrentSearch(): void {
    if (!this.hasSearched()) {
      this.search();
      return;
    }

    this.fetchPage(this.page(), { resetSelection: false, autoSelectFirst: false });
  }

  toggleKeyword(keyword: string): void {
    const normalizedKeyword = keyword.trim();

    if (normalizedKeyword.length === 0) {
      return;
    }

    if (this.isKeywordSelected(normalizedKeyword)) {
      this.removeKeyword(normalizedKeyword);
      return;
    }

    this.keywords.update((currentKeywords) => [
      ...currentKeywords,
      canonicalizeEuresKeyword(normalizedKeyword)
    ]);

    if (this.hasSearched()) {
      this.page.set(1);
      this.fetchPage(1, { resetSelection: true, autoSelectFirst: true });
    }
  }

  addKeywords(values: readonly string[]): void {
    const nextKeywords = [...this.keywords()];

    for (const value of values) {
      const normalizedKeyword = canonicalizeEuresKeyword(value);

      if (
        normalizedKeyword.length === 0 ||
        nextKeywords.some((keyword) => matchesEuresKeyword(keyword, normalizedKeyword))
      ) {
        continue;
      }

      nextKeywords.push(normalizedKeyword);
    }

    this.keywords.set(nextKeywords);
  }

  removeKeyword(keyword: string): void {
    this.keywords.update((currentKeywords) =>
      currentKeywords.filter((currentKeyword) => !matchesEuresKeyword(currentKeyword, keyword))
    );

    if (this.hasSearched() && this.keywords().length > 0) {
      this.page.set(1);
      this.fetchPage(1, { resetSelection: true, autoSelectFirst: true });
      return;
    }

    if (this.hasSearched() && this.keywords().length === 0) {
      this.cancelPendingRequests();
      this.resetResults();
      this.error.set('Select or add at least one keyword to search EURES.');
    }
  }

  clearKeywords(): void {
    this.keywords.set([]);

    if (this.hasSearched()) {
      this.cancelPendingRequests();
      this.resetResults();
      this.error.set('Select or add at least one keyword to search EURES.');
    }
  }

  isKeywordSelected(keyword: string): boolean {
    const canonicalKeyword = canonicalizeEuresKeyword(keyword);
    return this.keywords().some((currentKeyword) =>
      matchesEuresKeyword(currentKeyword, canonicalKeyword)
    );
  }

  goToPreviousPage(): void {
    this.fetchPage(this.page() - 1, { resetSelection: false, autoSelectFirst: true });
  }

  goToNextPage(): void {
    this.fetchPage(this.page() + 1, { resetSelection: false, autoSelectFirst: true });
  }

  goToPage(page: number): void {
    if (page < 1 || this.loading()) {
      return;
    }

    this.fetchPage(page, { resetSelection: false, autoSelectFirst: true });
  }

  updateResultsPerPage(value: number): void {
    if (!EURES_PAGE_SIZE_OPTIONS.includes(value as (typeof EURES_PAGE_SIZE_OPTIONS)[number])) {
      return;
    }

    if (this.resultsPerPage() === value) {
      return;
    }

    this.resultsPerPage.set(value);
    this.page.set(1);

    if (this.hasSearched()) {
      this.fetchPage(1, { resetSelection: true, autoSelectFirst: true });
    }
  }

  select(id: string): void {
    if (!id || this.selectedJobId() === id) {
      return;
    }

    this.resetSaveState();
    this.selectedJobId.set(id);
    this.detailLoading.set(true);
    this.detailError.set(null);
    this.selectedJob.set(null);
    this.detailSubscription?.unsubscribe();

    this.detailSubscription = this.apiService
      .getById(id, this.requestLanguage().trim() || 'en')
      .subscribe({
        next: (detail) => {
          this.selectedJob.set(detail);
          this.detailLoading.set(false);
          this.detailSubscription = null;
          this.syncSavedStateFromExistingJobs(detail);
        },
        error: (error: unknown) => {
          this.detailError.set(this.resolveDetailError(error));
          this.detailLoading.set(false);
          this.detailSubscription = null;
        }
      });
  }

  updateLocationCode(value: string): void {
    const normalizedLocation = normalizeEuresLocationCode(value);

    if (!isKnownEuresLocationCode(normalizedLocation)) {
      return;
    }

    if (this.locationCode() === normalizedLocation) {
      return;
    }

    this.locationCode.set(normalizedLocation);
    this.locationInitWarning.set(null);

    if (this.hasSearched()) {
      this.page.set(1);
      this.fetchPage(1, { resetSelection: true, autoSelectFirst: true });
    }
  }

  saveSelectedJob(): void {
    const selectedId = this.selectedJobId();
    const selectedJob = this.selectedJob();

    if (!selectedId || !selectedJob || this.saving() || this.savedJobId()) {
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);
    this.saveSubscription?.unsubscribe();

    this.saveSubscription = this.apiService
      .saveListing(selectedId, this.requestLanguage().trim() || 'en')
      .subscribe({
        next: (response) => {
          this.savedJobId.set(response.id);
          this.saveAlreadyExists.set(response.alreadyExists);
          this.saveError.set(null);
          this.saving.set(false);
          this.saveSubscription = null;
        },
        error: (error: unknown) => {
          this.saveError.set(this.resolveSaveError(error));
          this.saving.set(false);
          this.saveSubscription = null;
        }
      });
  }

  private syncSavedStateFromExistingJobs(detail: EuresJobDetail): void {
    const candidateUrls = [detail.applicationUrl, detail.sourceUrl]
      .map((url) => url?.trim())
      .filter((url): url is string => Boolean(url));

    if (candidateUrls.length === 0) {
      return;
    }

    const existingResult = this.jobResultsFacade
      .results()
      .find((result) => candidateUrls.includes(result.url.trim()));

    if (!existingResult) {
      return;
    }

    this.savedJobId.set(existingResult.id);
    this.saveAlreadyExists.set(true);
  }

  private resolveSaveError(error: unknown): string {
    return resolveHttpErrorMessage(error, {
      fallback: 'Could not save this listing to ApplyVault. Please try again.',
      statusMessages: {
        401: 'Sign in again to save EURES listings.',
        404: 'This listing was not found or is no longer available.',
        502: 'Saving is temporarily unavailable. Try again in a moment.'
      }
    });
  }

  private resetSaveState(): void {
    this.saveSubscription?.unsubscribe();
    this.saveSubscription = null;
    this.saving.set(false);
    this.saveError.set(null);
    this.savedJobId.set(null);
    this.saveAlreadyExists.set(false);
  }

  private fetchPage(page: number, options: FetchPageOptions): void {
    const normalizedKeywords = normalizeEuresKeywords(this.keywords());

    if (normalizedKeywords.length === 0) {
      this.error.set('Select or add at least one keyword to search EURES.');
      return;
    }

    if (!this.hasValidLocation()) {
      this.error.set('Select a valid country before searching EURES.');
      return;
    }

    this.keywords.set(normalizedKeywords);

    const targetPage = this.clampPage(page);
    this.cancelPendingSearch();
    this.loading.set(true);
    this.error.set(null);
    this.detailError.set(null);

    if (options.resetSelection) {
      this.cancelPendingDetail();
      this.selectedJobId.set(null);
      this.selectedJob.set(null);
    }

    const request = this.buildSearchRequest(normalizedKeywords, targetPage);

    this.searchSubscription = this.apiService.search(request).subscribe({
      next: (response) => {
        this.hasSearched.set(true);
        this.page.set(response.page);
        this.totalResults.set(response.totalResults);
        this.results.set(response.jobs);
        this.loading.set(false);
        this.searchSubscription = null;
        this.syncSelectionAfterSearch(response.jobs, options);
        this.searchGeneration.update((generation) => generation + 1);
      },
      error: (error: unknown) => {
        this.hasSearched.set(true);
        this.error.set(this.resolveSearchError(error));
        this.resetResults();
        this.loading.set(false);
        this.searchSubscription = null;
      }
    });
  }

  private buildSearchRequest(
    keywords: readonly string[],
    page: number
  ): EuresJobSearchRequest {
    return {
      keywords,
      locationCode: this.locationCode().trim() || EURES_DEFAULT_LOCATION_CODE,
      page,
      resultsPerPage: this.resultsPerPage(),
      requestLanguage: this.requestLanguage().trim() || 'en'
    };
  }

  private syncSelectionAfterSearch(
    jobs: readonly EuresJobListing[],
    options: FetchPageOptions
  ): void {
    if (options.selectJobId) {
      this.select(options.selectJobId);
      return;
    }

    const selectedId = this.selectedJobId();

    if (selectedId && jobs.some((job) => job.id === selectedId)) {
      if (!this.selectedJob() || this.selectedJob()?.id !== selectedId) {
        this.select(selectedId);
      }

      return;
    }

    if (options.autoSelectFirst && jobs.length > 0) {
      this.select(jobs[0].id);
      return;
    }

    if (selectedId && !jobs.some((job) => job.id === selectedId)) {
      this.cancelPendingDetail();
      this.selectedJobId.set(null);
      this.selectedJob.set(null);
    }
  }

  private clampPage(page: number): number {
    const totalPages = this.totalPages();
    return Math.max(1, totalPages > 0 ? Math.min(page, totalPages) : page);
  }

  private resolveSearchError(error: unknown): string {
    return resolveHttpErrorMessage(error, {
      fallback: 'EURES search failed. Check that the API is running and you are signed in.',
      statusMessages: {
        400: 'Invalid search request. Check your keywords and try again.',
        401: 'Sign in again to search EURES.',
        502: 'EURES search is temporarily unavailable. Try again in a moment.'
      }
    });
  }

  private resolveDetailError(error: unknown): string {
    return resolveHttpErrorMessage(error, {
      fallback: 'Could not load the selected job detail.',
      statusMessages: {
        404: 'This listing was not found or is no longer available.',
        401: 'Sign in again to view EURES job details.',
        502: 'EURES detail is temporarily unavailable. Try again in a moment.'
      }
    });
  }

  private resetResults(): void {
    this.results.set([]);
    this.totalResults.set(0);
    this.page.set(1);
    this.cancelPendingDetail();
    this.selectedJobId.set(null);
    this.selectedJob.set(null);
  }

  private resetState(): void {
    this.cancelPendingRequests();
    this.resetSaveState();
    this.keywords.set(['software']);
    this.locationCode.set(EURES_DEFAULT_LOCATION_CODE);
    this.locationInitWarning.set(null);
    this.resultsPerPage.set(EURES_DEFAULT_RESULTS_PER_PAGE);
    this.requestLanguage.set('en');
    this.loading.set(false);
    this.detailLoading.set(false);
    this.error.set(null);
    this.detailError.set(null);
    this.hasSearched.set(false);
    this.resetResults();
  }

  private cancelPendingSearch(): void {
    this.searchSubscription?.unsubscribe();
    this.searchSubscription = null;
  }

  private cancelPendingDetail(): void {
    this.detailSubscription?.unsubscribe();
    this.detailSubscription = null;
  }

  private cancelPendingRequests(): void {
    this.cancelPendingSearch();
    this.cancelPendingDetail();
    this.saveSubscription?.unsubscribe();
    this.saveSubscription = null;
  }
}
