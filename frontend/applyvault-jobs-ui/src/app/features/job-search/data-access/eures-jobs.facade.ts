import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ParamMap } from '@angular/router';
import { catchError, EMPTY, exhaustMap, map, of, Subject, switchMap, takeUntil } from 'rxjs';

import { resolveHttpErrorMessage } from '../../../core/http/api-error-message';
import { isRequestAborted } from '../../../core/http/is-request-aborted';
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
import { formatIndexedSearchSummary } from '../utils/job-search-results-summary.util';
import { getJobSearchProvider } from '../models/job-source.model';
import {
  canonicalizeEuresKeyword,
  matchesEuresKeyword,
  normalizeEuresKeywords
} from '../utils/eures-keyword.utils';
import {
  buildEuresUrlQueryParams,
  EuresUrlQueryParams
} from '../utils/eures-url-state.utils';
import {
  readEuresCountryFromQueryParams
} from '../utils/job-search-url-state.utils';
import { EuresJobsApiService } from './eures-jobs-api.service';

export const EURES_RESULTS_PER_PAGE = 5;

type FetchPageOptions = {
  resetSelection: boolean;
  autoSelectFirst: boolean;
  selectJobId?: string | null;
  append?: boolean;
};

type SearchIntent = { request: EuresJobSearchRequest; options: FetchPageOptions; epoch: number };
type DetailIntent = { id: string; language: string };
type SaveIntent = { id: string; language: string };

@Injectable()
export class EuresJobsFacade {
  private readonly authService = inject(AuthService);
  private readonly apiService = inject(EuresJobsApiService);
  private readonly jobResultsFacade = inject(JobResultsFacade);
  private readonly searchIntent$ = new Subject<SearchIntent>();
  private readonly searchCancel$ = new Subject<void>();
  private readonly detailIntent$ = new Subject<DetailIntent>();
  private readonly detailCancel$ = new Subject<void>();
  private readonly saveIntent$ = new Subject<SaveIntent>();
  private readonly saveCancel$ = new Subject<void>();
  private searchEpoch = 0;

  readonly resultsPerPage = signal(EURES_RESULTS_PER_PAGE);

  readonly keywords = signal<string[]>(['software']);
  readonly locationCode = signal(EURES_DEFAULT_LOCATION_CODE);
  readonly locationInitWarning = signal<string | null>(null);
  readonly requestLanguage = signal('en');
  readonly page = signal(1);
  readonly loading = signal(false);
  readonly loadingMore = signal(false);
  readonly loadMoreError = signal<string | null>(null);
  readonly detailLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly detailError = signal<string | null>(null);
  readonly totalResults = signal(0);
  readonly upstreamTotalResults = signal<number | null>(null);
  readonly resultsTruncated = signal(false);
  readonly results = signal<readonly EuresJobListing[]>([]);
  readonly selectedJobId = signal<string | null>(null);
  readonly selectedJob = signal<EuresJobDetail | null>(null);
  readonly hasSearched = signal(false);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly savedJobId = signal<string | null>(null);
  readonly saveAlreadyExists = signal(false);
  readonly searchGeneration = signal(0);
  readonly lastSearchedAt = signal<Date | null>(null);
  readonly pendingSelectedId = signal<string | null>(null);

  readonly keywordsLabel = computed(() => this.keywords().join(', '));

  readonly hasActiveSearch = computed(
    () => this.keywords().length > 0 && this.hasValidLocation()
  );

  readonly initialLoading = computed(() => this.loading() && this.results().length === 0);

  readonly resultsSummary = computed(() => {
    const provider = getJobSearchProvider('eures');

    if (!this.hasSearched()) {
      return provider.idleSearchPrompt;
    }

    if (this.loading() && !this.loadingMore()) {
      return provider.searchingPrompt;
    }

    const total = this.totalResults();

    if (total === 0) {
      return 'No matching listings';
    }

    return formatIndexedSearchSummary(this.results().length, total, this.keywordsLabel(), {
      upstreamTotal: this.upstreamTotalResults(),
      resultsTruncated: this.resultsTruncated(),
      sourceLabel: 'EURES'
    });
  });

  readonly hasMoreResults = computed(() => {
    const total = this.totalResults();

    if (total === 0) {
      return false;
    }

    return this.page() * this.resultsPerPage() < total;
  });

  readonly savedListingIds = computed(() => {
    const savedByUrl = new Map<string, string>();

    for (const result of this.jobResultsFacade.results()) {
      const url = result.url.trim();

      if (url) {
        savedByUrl.set(url, result.id);
      }
    }

    const listingToSaved = new Map<string, string>();

    for (const job of this.results()) {
      const sourceUrl = job.sourceUrl?.trim();

      if (sourceUrl && savedByUrl.has(sourceUrl)) {
        listingToSaved.set(job.id, savedByUrl.get(sourceUrl)!);
      }
    }

    return listingToSaved;
  });

  readonly hasValidLocation = computed(() => isKnownEuresLocationCode(this.locationCode()));

  constructor() {
    const destroyRef = inject(DestroyRef);

    this.searchIntent$
      .pipe(
        exhaustMap(({ request, options, epoch }) =>
          this.apiService.search(request).pipe(
            takeUntil(this.searchCancel$),
            map((response) => ({ kind: 'success' as const, response, options, epoch })),
            catchError((error: unknown) =>
              isRequestAborted(error)
                ? EMPTY
                : of({ kind: 'error' as const, error, options, epoch })
            )
          )
        ),
        takeUntilDestroyed(destroyRef)
      )
      .subscribe((result) => {
        if (result.epoch !== this.searchEpoch) {
          return;
        }

        if (result.kind === 'error') {
          const message = this.resolveSearchError(result.error);

          if (result.options.append) {
            this.loadMoreError.set(message);
            this.loadingMore.set(false);
            return;
          }

          this.hasSearched.set(true);
          this.error.set(message);
          this.resetResults();
          this.loading.set(false);
          this.loadingMore.set(false);
          return;
        }

        const { response, options } = result;
        const mergedResults = this.mergeResults(this.results(), response.jobs, options.append);

        if (options.append && mergedResults.length === this.results().length) {
          this.totalResults.set(this.results().length);
          this.loadingMore.set(false);
          this.loadMoreError.set(null);
          return;
        }

        this.hasSearched.set(true);
        this.page.set(response.page);
        this.totalResults.set(response.totalResults);
        this.upstreamTotalResults.set(response.upstreamTotalResults ?? null);
        this.resultsTruncated.set(response.resultsTruncated === true);
        this.results.set(mergedResults);
        this.loading.set(false);
        this.loadingMore.set(false);
        this.loadMoreError.set(null);
        this.lastSearchedAt.set(new Date());
        this.syncSelectionAfterSearch(
          options.append ? this.results() : response.jobs,
          options
        );

        if (!options.append) {
          this.searchGeneration.update((generation) => generation + 1);
        }

        if (this.authService.session()) {
          this.jobResultsFacade.load();
        }
      });

    this.detailIntent$
      .pipe(
        switchMap(({ id, language }) =>
          this.apiService.getById(id, language).pipe(
            takeUntil(this.detailCancel$),
            map((detail) => ({ kind: 'success' as const, detail })),
            catchError((error: unknown) =>
              isRequestAborted(error) ? EMPTY : of({ kind: 'error' as const, error })
            )
          )
        ),
        takeUntilDestroyed(destroyRef)
      )
      .subscribe((result) => {
        if (result.kind === 'error') {
          this.detailError.set(this.resolveDetailError(result.error));
          this.detailLoading.set(false);
          return;
        }

        this.selectedJob.set(result.detail);
        this.detailLoading.set(false);
        this.syncSavedStateFromExistingJobs(result.detail);
      });

    this.saveIntent$
      .pipe(
        switchMap(({ id, language }) =>
          this.apiService.saveListing(id, language).pipe(
            takeUntil(this.saveCancel$),
            map((response) => ({ kind: 'success' as const, response })),
            catchError((error: unknown) =>
              isRequestAborted(error) ? EMPTY : of({ kind: 'error' as const, error })
            )
          )
        ),
        takeUntilDestroyed(destroyRef)
      )
      .subscribe((result) => {
        if (result.kind === 'error') {
          this.saveError.set(this.resolveSaveError(result.error));
          this.saving.set(false);
          return;
        }

        this.savedJobId.set(result.response.id);
        this.saveAlreadyExists.set(result.response.alreadyExists);
        this.saveError.set(null);
        this.saving.set(false);
        void this.jobResultsFacade.load();
      });

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

    if (params.get('source')?.trim().toLowerCase() !== 'eures' && params.get('source')?.trim()) {
      return;
    }

    const country = readEuresCountryFromQueryParams(params);

    if (country) {
      this.locationCode.set(country);
      this.locationInitWarning.set(null);
      return;
    }

    const legacyLocationParam = params.get('location');

    if (legacyLocationParam?.trim()) {
      this.locationCode.set(EURES_DEFAULT_LOCATION_CODE);
      this.locationInitWarning.set(
        `Unknown country code "${legacyLocationParam.trim()}". Using Denmark instead.`
      );
    }
  }

  loadInitialSearch(selectJobId?: string | null): void {
    if (!this.authService.session() || this.hasSearched()) {
      return;
    }

    if (!this.hasValidLocation()) {
      return;
    }

    this.fetchPage(1, {
      resetSelection: !selectJobId,
      autoSelectFirst: !selectJobId,
      selectJobId: selectJobId ?? null
    });
  }

  restoreFromUrlState(selectJobId?: string | null): void {
    if (!this.authService.session() || !this.hasValidLocation()) {
      return;
    }

    this.fetchPage(1, {
      resetSelection: !selectJobId,
      autoSelectFirst: !selectJobId,
      selectJobId: selectJobId ?? null
    });
  }

  buildQueryParamState(): EuresUrlQueryParams {
    return buildEuresUrlQueryParams({
      keywords: this.keywords(),
      locationCode: this.locationCode(),
      selectedJobId: this.selectedJobId()
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

    this.fetchPage(1, { resetSelection: false, autoSelectFirst: false });
  }

  loadMore(): void {
    if (this.loading() || this.loadingMore() || !this.hasMoreResults()) {
      return;
    }

    this.loadMoreError.set(null);
    this.fetchPage(this.page() + 1, {
      resetSelection: false,
      autoSelectFirst: false,
      append: true
    });
  }

  isListingSaved(id: string): boolean {
    return this.savedListingIds().has(id);
  }

  savedJobIdForListing(id: string): string | null {
    return this.savedListingIds().get(id) ?? null;
  }

  selectWhenLoaded(id: string): void {
    const normalizedId = id.trim();

    if (!normalizedId) {
      return;
    }

    this.pendingSelectedId.set(null);

    if (this.results().some((job) => job.id === normalizedId)) {
      this.select(normalizedId);
      return;
    }

    if (this.hasSearched()) {
      this.select(normalizedId);
    }
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

  select(id: string): void {
    if (!id || this.selectedJobId() === id) {
      return;
    }

    this.resetSaveState();
    this.selectedJobId.set(id);
    this.detailLoading.set(true);
    this.detailError.set(null);
    this.selectedJob.set(null);

    this.detailIntent$.next({
      id,
      language: this.requestLanguage().trim() || 'en'
    });
  }

  clearSelection(): void {
    this.cancelPendingDetail();
    this.resetSaveState();
    this.selectedJobId.set(null);
    this.selectedJob.set(null);
    this.detailLoading.set(false);
    this.detailError.set(null);
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

    this.saveIntent$.next({
      id: selectedId,
      language: this.requestLanguage().trim() || 'en'
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
    this.saveCancel$.next();
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

    const targetPage = Math.max(1, page);
    const isAppend = options.append === true;

    if (isAppend) {
      this.loadingMore.set(true);
    } else {
      this.cancelPendingSearch();
      this.searchEpoch += 1;
      this.clearPagedResults();
      this.loading.set(true);
      this.error.set(null);
    }

    this.detailError.set(null);

    if (options.resetSelection) {
      this.cancelPendingDetail();
      this.selectedJobId.set(null);
      this.selectedJob.set(null);
    }

    const request = this.buildSearchRequest(normalizedKeywords, targetPage);

    this.searchIntent$.next({
      request,
      options: { ...options, append: isAppend },
      epoch: this.searchEpoch
    });
  }

  private clearPagedResults(): void {
    this.results.set([]);
    this.totalResults.set(0);
    this.upstreamTotalResults.set(null);
    this.resultsTruncated.set(false);
    this.page.set(1);
  }

  private mergeResults(
    current: readonly EuresJobListing[],
    incoming: readonly EuresJobListing[],
    append?: boolean
  ): readonly EuresJobListing[] {
    if (!append) {
      return incoming;
    }

    const existingIds = new Set(current.map((job) => job.id));
    const uniqueIncoming = incoming.filter((job) => !existingIds.has(job.id));

    return [...current, ...uniqueIncoming];
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
    const pendingId = this.pendingSelectedId();

    if (pendingId && jobs.some((job) => job.id === pendingId)) {
      this.pendingSelectedId.set(null);
      this.select(pendingId);
      return;
    }

    if (options.selectJobId) {
      this.pendingSelectedId.set(null);
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
    this.clearPagedResults();
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
    this.resultsPerPage.set(EURES_RESULTS_PER_PAGE);
    this.requestLanguage.set('en');
    this.loading.set(false);
    this.loadingMore.set(false);
    this.loadMoreError.set(null);
    this.detailLoading.set(false);
    this.error.set(null);
    this.detailError.set(null);
    this.hasSearched.set(false);
    this.lastSearchedAt.set(null);
    this.pendingSelectedId.set(null);
    this.resetResults();
  }

  private cancelPendingSearch(): void {
    this.searchCancel$.next();
  }

  private cancelPendingDetail(): void {
    this.detailCancel$.next();
  }

  private cancelPendingRequests(): void {
    this.cancelPendingSearch();
    this.cancelPendingDetail();
    this.saveCancel$.next();
  }
}
