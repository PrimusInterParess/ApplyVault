import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { resolveHttpErrorMessage } from '../../../core/http/api-error-message';
import { AuthService } from '../../../core/auth/auth.service';
import {
  EuresJobDetail,
  EuresJobListing,
  EuresJobSearchRequest
} from '../models/eures-job.model';
import {
  canonicalizeEuresKeyword,
  matchesEuresKeyword,
  normalizeEuresKeywords
} from '../utils/eures-keyword.utils';
import { EuresJobsApiService } from './eures-jobs-api.service';

@Injectable()
export class EuresJobsFacade {
  private readonly authService = inject(AuthService);
  private readonly apiService = inject(EuresJobsApiService);
  private searchSubscription: Subscription | null = null;
  private detailSubscription: Subscription | null = null;

  readonly resultsPerPage = 4;

  readonly keywords = signal<string[]>(['software']);
  readonly locationCode = signal('dk');
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

  readonly keywordsLabel = computed(() => this.keywords().join(', '));

  readonly totalPages = computed(() => {
    const total = this.totalResults();

    if (total <= 0) {
      return 0;
    }

    return Math.ceil(total / this.resultsPerPage);
  });

  readonly pageRangeLabel = computed(() => {
    const total = this.totalResults();
    const currentPage = this.page();

    if (total <= 0) {
      return '';
    }

    const start = (currentPage - 1) * this.resultsPerPage + 1;
    const end = Math.min(currentPage * this.resultsPerPage, total);
    return `${start}-${end} of ${total}`;
  });

  readonly canGoToPreviousPage = computed(() => this.page() > 1 && !this.loading());
  readonly canGoToNextPage = computed(
    () => this.page() < this.totalPages() && !this.loading()
  );

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

  initialize(): void {
    if (!this.authService.session() || this.hasSearched()) {
      return;
    }

    this.search();
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

  select(id: string): void {
    if (!id || this.selectedJobId() === id) {
      return;
    }

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
        },
        error: (error: unknown) => {
          this.detailError.set(this.resolveDetailError(error));
          this.detailLoading.set(false);
          this.detailSubscription = null;
        }
      });
  }

  updateLocationCode(value: string): void {
    this.locationCode.set(value.trim().toLowerCase() || 'dk');
  }

  private fetchPage(
    page: number,
    options: { resetSelection: boolean; autoSelectFirst: boolean }
  ): void {
    const normalizedKeywords = normalizeEuresKeywords(this.keywords());

    if (normalizedKeywords.length === 0) {
      this.error.set('Select or add at least one keyword to search EURES.');
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
        this.syncSelectionAfterSearch(response.jobs, options.autoSelectFirst);
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
      locationCode: this.locationCode().trim() || 'dk',
      page,
      resultsPerPage: this.resultsPerPage,
      requestLanguage: this.requestLanguage().trim() || 'en'
    };
  }

  private syncSelectionAfterSearch(
    jobs: readonly EuresJobListing[],
    autoSelectFirst: boolean
  ): void {
    const selectedId = this.selectedJobId();

    if (selectedId && jobs.some((job) => job.id === selectedId)) {
      if (!this.selectedJob() || this.selectedJob()?.id !== selectedId) {
        this.select(selectedId);
      }

      return;
    }

    if (autoSelectFirst && jobs.length > 0) {
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
    this.keywords.set(['software']);
    this.locationCode.set('dk');
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
  }
}
