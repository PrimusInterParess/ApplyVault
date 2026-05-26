import { CommonModule } from '@angular/common';
import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  OnInit,
  signal,
  viewChild
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { filter, skip } from 'rxjs';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { JobResultsFacade } from '../../../job-results/data-access/job-results.facade';
import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import {
  EURES_PAGE_SIZE_OPTIONS,
  EuresJobsFacade
} from '../../data-access/eures-jobs.facade';
import { EURES_KEYWORD_SUGGESTION_GROUPS } from '../../models/eures-keyword-suggestions';
import { EURES_LOCATION_OPTIONS } from '../../models/eures-location-options';
import { EuresJobCardComponent } from '../../presentation/eures-job-card/eures-job-card.component';
import { EuresJobDetailComponent } from '../../presentation/eures-job-detail/eures-job-detail.component';
import { euresQueryParamsEqual } from '../../utils/eures-url-state.utils';

@Component({
  selector: 'app-eures-jobs-page',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    SkeletonBlockComponent,
    EuresJobCardComponent,
    EuresJobDetailComponent
  ],
  providers: [EuresJobsFacade],
  templateUrl: './eures-jobs-page.component.html',
  styleUrl: './eures-jobs-page.component.scss'
})
export class EuresJobsPageComponent implements OnInit {
  readonly facade = inject(EuresJobsFacade);
  private readonly jobResultsFacade = inject(JobResultsFacade);
  readonly keywordSuggestionGroups = EURES_KEYWORD_SUGGESTION_GROUPS;
  readonly locationOptions = EURES_LOCATION_OPTIONS;
  readonly pageSizeOptions = EURES_PAGE_SIZE_OPTIONS;
  readonly skeletonCardCount = [0, 1, 2, 3, 4, 5];
  readonly readInputValue = readInputValue;

  protected readonly draftKeyword = signal('');
  protected readonly jumpToPageValue = signal('');
  protected readonly mobileDetailEngaged = signal(false);
  protected readonly loadBannerMessage = signal('');
  protected readonly pageAnnouncement = signal('');
  protected readonly listRegion = viewChild<ElementRef<HTMLElement>>('listRegion');

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private suppressUrlSync = false;
  private lastSyncedQueryKey = '';
  private lastFocusedGeneration = 0;
  private shouldFocusAfterSearch = false;
  private bannerDismissHandle: ReturnType<typeof setTimeout> | null = null;
  private wasLoading = false;

  protected readonly skeletonListIndexes = computed(() =>
    Array.from({ length: this.facade.resultsPerPage() }, (_, index) => index)
  );

  protected readonly searchDisabledHint = computed(() => {
    if (!this.facade.hasValidLocation()) {
      return 'Select a valid country to search.';
    }

    if (this.facade.keywords().length === 0 && this.draftKeyword().trim().length === 0) {
      return 'Add at least one keyword or select a popular search term.';
    }

    return '';
  });

  constructor() {
    effect(() => {
      const loading = this.facade.loading();
      const error = this.facade.error();
      const generation = this.facade.searchGeneration();

      if (this.wasLoading && !loading && !error && generation > 0) {
        const total = this.facade.totalResults();
        this.showLoadBanner(
          total === 0
            ? 'No matching listings found'
            : `${total} matching ${total === 1 ? 'listing' : 'listings'} found`
        );
      }

      this.wasLoading = loading;
    });

    effect(() => {
      const announcement = this.facade.pageAnnouncement();

      if (announcement) {
        this.pageAnnouncement.set(announcement);
      }
    });

    effect(() => {
      const generation = this.facade.searchGeneration();

      if (generation === 0 || this.facade.loading() || !this.shouldFocusAfterSearch) {
        return;
      }

      if (generation === this.lastFocusedGeneration) {
        return;
      }

      this.lastFocusedGeneration = generation;
      this.shouldFocusAfterSearch = false;

      afterNextRender(() => this.focusAfterSearch());
    });

    effect(() => {
      this.facade.page();

      if (this.facade.hasSearched() && !this.facade.initialLoading()) {
        afterNextRender(() => this.scrollListToTop());
      }
    });
  }

  ngOnInit(): void {
    this.jobResultsFacade.load();
    this.applyRouteParams(this.route.snapshot.queryParamMap, { triggerSearch: true });

    const selectedId = this.route.snapshot.queryParamMap.get('selected');

    if (selectedId) {
      this.mobileDetailEngaged.set(true);
      this.facade.selectWhenLoaded(selectedId);
    }

    this.route.queryParamMap
      .pipe(
        skip(1),
        filter(() => !this.suppressUrlSync),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((params) => {
        const selected = params.get('selected');

        if (selected && selected !== this.facade.selectedJobId()) {
          this.mobileDetailEngaged.set(true);
          this.facade.selectWhenLoaded(selected);
        }

        this.applyRouteParams(params, { triggerSearch: true });
      });
  }

  protected lastSearchedLabel(): string | null {
    const searchedAt = this.facade.lastSearchedAt();

    if (!searchedAt) {
      return null;
    }

    return `Last searched ${searchedAt.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}`;
  }

  protected dismissLoadBanner(): void {
    this.loadBannerMessage.set('');
  }

  protected updateDraftKeyword(event: Event): void {
    this.draftKeyword.set(readInputValue(event));
  }

  protected clearDraftKeyword(): void {
    this.draftKeyword.set('');
  }

  protected updateLocationCode(event: Event): void {
    this.facade.updateLocationCode(readInputValue(event));
    this.syncUrlIfNeeded();
  }

  protected updatePageSize(event: Event): void {
    const parsedPageSize = Number.parseInt(readInputValue(event), 10);

    if (Number.isNaN(parsedPageSize)) {
      return;
    }

    this.facade.updateResultsPerPage(parsedPageSize);
    this.syncUrlIfNeeded();
  }

  protected updateJumpToPageValue(event: Event): void {
    this.jumpToPageValue.set(readInputValue(event));
  }

  protected toggleSuggestion(keyword: string): void {
    this.facade.toggleKeyword(keyword);
    this.syncUrlIfNeeded();
  }

  protected isActiveSuggestion(keyword: string): boolean {
    return this.facade.isKeywordSelected(keyword);
  }

  protected runSearch(): void {
    if (!this.canSearch()) {
      return;
    }

    this.mobileDetailEngaged.set(false);
    this.shouldFocusAfterSearch = true;
    this.facade.search(this.draftKeyword());
    this.draftKeyword.set('');
    this.syncUrlIfNeeded();
  }

  protected runSearchFromKeyboard(event: Event): void {
    event.preventDefault();
    this.runSearch();
  }

  protected canSearch(): boolean {
    return (
      this.facade.hasValidLocation() &&
      (this.facade.keywords().length > 0 || this.draftKeyword().trim().length > 0) &&
      !this.facade.loading()
    );
  }

  protected removeKeyword(keyword: string): void {
    this.facade.removeKeyword(keyword);
    this.syncUrlIfNeeded();
  }

  protected clearKeywords(): void {
    this.facade.clearKeywords();
    this.syncUrlIfNeeded();
  }

  protected retrySearch(): void {
    this.shouldFocusAfterSearch = true;
    this.facade.refreshCurrentSearch();
    this.syncUrlIfNeeded();
  }

  protected saveSelectedJob(): void {
    this.facade.saveSelectedJob();
  }

  protected selectJob(id: string): void {
    this.mobileDetailEngaged.set(true);
    this.facade.select(id);
    this.syncUrlIfNeeded();
  }

  protected showMobileDetail(): boolean {
    return this.mobileDetailEngaged() && this.facade.selectedJobId() !== null;
  }

  protected backToList(): void {
    this.mobileDetailEngaged.set(false);
    this.facade.clearSelection();
    this.syncUrlIfNeeded();
  }

  protected goToPreviousPage(): void {
    this.facade.goToPreviousPage();
    this.syncUrlIfNeeded();
  }

  protected goToNextPage(): void {
    this.facade.goToNextPage();
    this.syncUrlIfNeeded();
  }

  protected jumpToPage(event: Event): void {
    event.preventDefault();

    const parsedPage = Number.parseInt(this.jumpToPageValue().trim(), 10);

    if (Number.isNaN(parsedPage)) {
      return;
    }

    this.facade.goToPage(parsedPage);
    this.jumpToPageValue.set('');
    this.syncUrlIfNeeded();
  }

  protected showJumpToPage(): boolean {
    return this.facade.totalPages() > 5;
  }

  protected locationLabel(code: string): string {
    return this.locationOptions.find((option) => option.code === code)?.label ?? code;
  }

  protected retryDetail(): void {
    const selectedId = this.facade.selectedJobId();

    if (selectedId) {
      this.selectJob(selectedId);
    }
  }

  protected detailUrl(): string | null {
    const selectedJob = this.facade.selectedJob();

    if (!selectedJob) {
      return null;
    }

    return selectedJob.applicationUrl ?? selectedJob.sourceUrl;
  }

  protected handleListKeydown(event: KeyboardEvent): void {
    const listElement = this.listRegion()?.nativeElement;

    if (!listElement || !(event.target instanceof HTMLElement) || !listElement.contains(event.target)) {
      return;
    }

    const cards = Array.from(
      listElement.querySelectorAll<HTMLButtonElement>('.job-card:not([disabled])')
    );

    if (cards.length === 0) {
      return;
    }

    const activeIndex = cards.findIndex(
      (card) => card === document.activeElement || card.classList.contains('job-card--selected')
    );
    const resolvedIndex = activeIndex >= 0 ? activeIndex : 0;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      const nextIndex = Math.min(resolvedIndex + 1, cards.length - 1);
      this.focusCard(cards, nextIndex);
      return;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      const nextIndex = Math.max(resolvedIndex - 1, 0);
      this.focusCard(cards, nextIndex);
    }
  }

  private focusCard(cards: readonly HTMLButtonElement[], index: number): void {
    const card = cards[index];
    const jobId = card.dataset['jobId'];

    if (jobId) {
      this.mobileDetailEngaged.set(true);
      this.facade.select(jobId);
      this.syncUrlIfNeeded();
    }

    card.focus();
  }

  private showLoadBanner(message: string): void {
    if (this.bannerDismissHandle !== null) {
      clearTimeout(this.bannerDismissHandle);
    }

    this.loadBannerMessage.set(message);

    this.bannerDismissHandle = setTimeout(() => {
      this.loadBannerMessage.set('');
      this.bannerDismissHandle = null;
    }, 4000);
  }

  private focusAfterSearch(): void {
    const listElement = this.listRegion()?.nativeElement;

    if (!listElement) {
      return;
    }

    const firstCard = listElement.querySelector<HTMLButtonElement>('.job-card');

    if (firstCard) {
      listElement.focus();
      return;
    }

    listElement.focus();
  }

  private scrollListToTop(): void {
    const listElement = this.listRegion()?.nativeElement;

    if (listElement) {
      listElement.scrollTop = 0;
    }
  }

  private applyRouteParams(params: ParamMap, options: { triggerSearch: boolean }): void {
    this.facade.initFromQueryParams(params);

    if (!options.triggerSearch) {
      return;
    }

    const selected = params.get('selected');
    const queryKey = this.buildQueryKey();

    if (!this.facade.hasSearched()) {
      this.facade.loadInitialSearch(selected);
      return;
    }

    if (queryKey === this.lastSyncedQueryKey) {
      if (selected) {
        this.facade.selectWhenLoaded(selected);
      }

      return;
    }

    this.facade.restoreFromUrlState(selected);
  }

  private buildQueryKey(): string {
    return JSON.stringify(this.facade.buildQueryParamState());
  }

  private syncUrlIfNeeded(): void {
    if (this.suppressUrlSync) {
      return;
    }

    const queryParams = this.facade.buildQueryParamState();
    const queryKey = JSON.stringify(queryParams);

    if (euresQueryParamsEqual(this.route.snapshot.queryParamMap, queryParams)) {
      this.lastSyncedQueryKey = queryKey;
      return;
    }

    this.lastSyncedQueryKey = queryKey;
    this.suppressUrlSync = true;

    void this.router
      .navigate([], {
        relativeTo: this.route,
        queryParams,
        queryParamsHandling: 'merge',
        replaceUrl: true
      })
      .finally(() => {
        this.suppressUrlSync = false;
      });
  }
}
