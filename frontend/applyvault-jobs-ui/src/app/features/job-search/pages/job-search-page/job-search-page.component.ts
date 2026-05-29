import { CommonModule } from '@angular/common';
import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  EnvironmentInjector,
  HostListener,
  inject,
  OnInit,
  runInInjectionContext,
  signal,
  viewChild
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { filter, skip } from 'rxjs';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { JobResultsFacade } from '../../../job-results/data-access/job-results.facade';
import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import { EuresJobsFacade } from '../../data-access/eures-jobs.facade';
import { JobnetJobsFacade } from '../../data-access/jobnet-jobs.facade';
import { JobSearchFacade } from '../../data-access/job-search.facade';
import { EURES_KEYWORD_SUGGESTION_GROUPS } from '../../models/eures-keyword-suggestions';
import { EURES_LOCATION_OPTIONS } from '../../models/eures-location-options';
import { filterKeywordSuggestionGroupsForSource } from '../../models/job-search-filters.model';
import {
  hasMultipleJobSearchProviders,
  JobSearchSource
} from '../../models/job-source.model';
import { ExternalJobCardComponent } from '../../presentation/external-job-card/external-job-card.component';
import { ExternalJobDetailComponent } from '../../presentation/external-job-detail/external-job-detail.component';
import { JobSearchSourceToggleComponent } from '../../presentation/job-search-source-toggle/job-search-source-toggle.component';
import {
  buildJobSearchQueryKeyFromParams,
  jobSearchQueryParamsEqual
} from '../../utils/job-search-url-state.utils';

@Component({
  selector: 'app-job-search-page',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    SkeletonBlockComponent,
    ExternalJobCardComponent,
    ExternalJobDetailComponent,
    JobSearchSourceToggleComponent
  ],
  providers: [EuresJobsFacade, JobnetJobsFacade, JobSearchFacade],
  templateUrl: './job-search-page.component.html',
  styleUrl: './job-search-page.component.scss'
})
export class JobSearchPageComponent implements OnInit {
  readonly facade = inject(JobSearchFacade);
  private readonly jobResultsFacade = inject(JobResultsFacade);
  readonly keywordSuggestionGroups = EURES_KEYWORD_SUGGESTION_GROUPS;
  readonly locationOptions = EURES_LOCATION_OPTIONS;
  readonly loadMoreSkeletonIndexes = [0, 1];
  readonly readInputValue = readInputValue;
  readonly showSourceToggle = hasMultipleJobSearchProviders();

  protected readonly draftKeyword = signal('');
  protected readonly activeSuggestionGroup = signal('');
  protected readonly mobileDetailEngaged = signal(false);
  protected readonly loadBannerMessage = signal('');
  protected readonly listRegion = viewChild<ElementRef<HTMLElement>>('listRegion');

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(EnvironmentInjector);
  private suppressUrlSync = false;
  private pendingUrlSyncKey: string | null = null;
  private lastSyncedQueryKey = '';
  private lastFocusedGeneration = 0;
  private shouldFocusAfterSearch = false;
  private bannerDismissHandle: ReturnType<typeof setTimeout> | null = null;
  private wasLoading = false;

  protected readonly skeletonListIndexes = computed(() =>
    Array.from({ length: 5 }, (_, index) => index)
  );

  protected readonly searchDisabledHint = computed(() => {
    if (this.facade.source() === 'eures' && !this.facade.hasValidLocation()) {
      return 'Select a valid country to search.';
    }

    if (this.facade.keywords().length === 0 && this.draftKeyword().trim().length === 0) {
      return 'Add at least one keyword or select a popular search term.';
    }

    return '';
  });

  protected readonly visibleSuggestionGroups = computed(() => {
    const sourceFiltered = filterKeywordSuggestionGroupsForSource(
      this.keywordSuggestionGroups,
      this.facade.source()
    );
    const activeGroup = this.activeSuggestionGroup();

    if (!activeGroup) {
      return sourceFiltered;
    }

    return sourceFiltered.filter((group) => group.label === activeGroup);
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
      const generation = this.facade.searchGeneration();

      if (generation === 0 || this.facade.loading() || !this.shouldFocusAfterSearch) {
        return;
      }

      if (generation === this.lastFocusedGeneration) {
        return;
      }

      this.lastFocusedGeneration = generation;
      this.shouldFocusAfterSearch = false;

      runInInjectionContext(this.injector, () => {
        afterNextRender(() => {
          this.scrollListToTop();
          this.focusAfterSearch();
          this.prefetchIfListShort();
        });
      });
    });

    effect(() => {
      this.facade.results().length;
      this.facade.loadingMore();
      this.facade.loading();

      if (
        !this.facade.hasSearched() ||
        this.facade.loading() ||
        this.facade.loadingMore() ||
        !this.facade.hasMoreResults()
      ) {
        return;
      }

      runInInjectionContext(this.injector, () => {
        afterNextRender(() => this.prefetchIfListShort());
      });
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

  protected onSourceChange(nextSource: JobSearchSource): void {
    if (this.facade.source() === nextSource) {
      return;
    }

    const shouldRunSearch =
      this.facade.hasSearched() || this.facade.keywords().length > 0;

    this.mobileDetailEngaged.set(false);
    this.resetSuggestionGroupForSource(nextSource);
    this.facade.setSource(nextSource);
    this.syncUrlIfNeeded();

    if (shouldRunSearch) {
      this.shouldFocusAfterSearch = true;
      this.facade.refreshCurrentSearch();
    }
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

  protected updateSuggestionGroup(event: Event): void {
    this.activeSuggestionGroup.set(readInputValue(event));
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
    const hasKeywords =
      this.facade.keywords().length > 0 || this.draftKeyword().trim().length > 0;
    const hasValidLocation =
      this.facade.source() === 'jobnet' || this.facade.hasValidLocation();

    return hasValidLocation && hasKeywords && !this.facade.loading();
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

  protected loadMore(): void {
    this.facade.loadMore();
  }

  protected retryLoadMore(): void {
    this.facade.loadMore();
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

  protected locationLabel(code: string | null): string {
    if (!code) {
      return 'any location';
    }

    return this.facade.locationLabel(code);
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

  protected searchActionLabel(): string {
    if (this.facade.loading()) {
      return 'Searching...';
    }

    return this.facade.searchActionLabel();
  }

  @HostListener('window:scroll')
  protected onListScroll(): void {
    if (!this.facade.hasSearched() || this.facade.loading() || this.facade.loadingMore()) {
      return;
    }

    const thresholdPx = 160;
    const list = this.listRegion()?.nativeElement;

    if (list && list.scrollHeight > list.clientHeight + 8) {
      const listNearBottom =
        list.scrollTop + list.clientHeight >= list.scrollHeight - thresholdPx;

      if (listNearBottom) {
        this.facade.loadMore();
      }

      return;
    }

    const documentElement = document.documentElement;
    const windowNearBottom =
      window.scrollY + window.innerHeight >= documentElement.scrollHeight - thresholdPx;

    if (windowNearBottom) {
      this.facade.loadMore();
    }
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

  private resetSuggestionGroupForSource(source: JobSearchSource): void {
    const activeGroup = this.activeSuggestionGroup();

    if (!activeGroup) {
      return;
    }

    const visibleLabels = filterKeywordSuggestionGroupsForSource(
      this.keywordSuggestionGroups,
      source
    ).map((group) => group.label);

    if (!visibleLabels.includes(activeGroup)) {
      this.activeSuggestionGroup.set('');
    }
  }

  private prefetchIfListShort(): void {
    const list = this.listRegion()?.nativeElement;

    if (!list || !this.facade.hasMoreResults() || this.facade.loading() || this.facade.loadingMore()) {
      return;
    }

    const listFillsViewport = list.scrollHeight <= list.clientHeight + 8;

    if (listFillsViewport) {
      this.facade.loadMore();
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

    if (listElement) {
      listElement.focus();
    }
  }

  private scrollListToTop(): void {
    const listElement = this.listRegion()?.nativeElement;

    if (listElement) {
      listElement.scrollTop = 0;
    }
  }

  private applyRouteParams(params: ParamMap, options: { triggerSearch: boolean }): void {
    const urlQueryKey = buildJobSearchQueryKeyFromParams(params);

    if (this.pendingUrlSyncKey !== null && urlQueryKey !== this.pendingUrlSyncKey) {
      return;
    }

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

    if (this.pendingUrlSyncKey !== null && queryKey === this.pendingUrlSyncKey) {
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

    if (jobSearchQueryParamsEqual(this.route.snapshot.queryParamMap, queryParams)) {
      this.lastSyncedQueryKey = queryKey;
      return;
    }

    this.pendingUrlSyncKey = queryKey;
    this.suppressUrlSync = true;

    void this.router
      .navigate([], {
        relativeTo: this.route,
        queryParams,
        queryParamsHandling: 'merge',
        replaceUrl: true
      })
      .finally(() => {
        this.pendingUrlSyncKey = null;
        this.lastSyncedQueryKey = queryKey;
        this.suppressUrlSync = false;
        this.applyRouteParams(this.route.snapshot.queryParamMap, { triggerSearch: false });
      });
  }
}
