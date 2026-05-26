import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, effect, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { filter, skip } from 'rxjs';

import { SafeHtmlPipe } from '../../../../core/html/safe-html.pipe';
import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import {
  EURES_PAGE_SIZE_OPTIONS,
  EuresJobsFacade
} from '../../data-access/eures-jobs.facade';
import { EURES_KEYWORD_SUGGESTION_GROUPS } from '../../models/eures-keyword-suggestions';
import { EURES_LOCATION_OPTIONS } from '../../models/eures-location-options';
import { euresQueryParamsEqual } from '../../utils/eures-url-state.utils';

@Component({
  selector: 'app-eures-jobs-page',
  standalone: true,
  imports: [CommonModule, SafeHtmlPipe, RouterLink, SkeletonBlockComponent],
  providers: [EuresJobsFacade],
  templateUrl: './eures-jobs-page.component.html',
  styleUrl: './eures-jobs-page.component.scss'
})
export class EuresJobsPageComponent implements OnInit {
  readonly facade = inject(EuresJobsFacade);
  readonly keywordSuggestionGroups = EURES_KEYWORD_SUGGESTION_GROUPS;
  readonly locationOptions = EURES_LOCATION_OPTIONS;
  readonly pageSizeOptions = EURES_PAGE_SIZE_OPTIONS;
  readonly draftKeyword = signal('');
  readonly jumpToPageValue = signal('');
  protected readonly skeletonListIndexes = computed(() =>
    Array.from({ length: this.facade.resultsPerPage() }, (_, index) => index)
  );

  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private suppressUrlSync = false;
  private lastSyncedQueryKey = '';

  constructor() {
    effect(() => {
      this.facade.searchGeneration();

      if (this.facade.hasSearched()) {
        this.syncUrlIfNeeded();
      }
    });
  }

  protected asValue(event: Event): string {
    return (event.target as HTMLInputElement | HTMLSelectElement | null)?.value ?? '';
  }

  protected updateDraftKeyword(event: Event): void {
    this.draftKeyword.set(this.asValue(event));
  }

  protected updateLocationCode(event: Event): void {
    this.facade.updateLocationCode(this.asValue(event));
  }

  protected updatePageSize(event: Event): void {
    const parsedPageSize = Number.parseInt(this.asValue(event), 10);

    if (Number.isNaN(parsedPageSize)) {
      return;
    }

    this.facade.updateResultsPerPage(parsedPageSize);
    this.syncUrlIfNeeded();
  }

  protected updateJumpToPageValue(event: Event): void {
    this.jumpToPageValue.set(this.asValue(event));
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
      (this.facade.keywords().length > 0 || this.draftKeyword().trim().length > 0)
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
    this.facade.refreshCurrentSearch();
    this.syncUrlIfNeeded();
  }

  protected saveSelectedJob(): void {
    this.facade.saveSelectedJob();
  }

  protected selectJob(id: string): void {
    this.facade.select(id);
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

  protected detailUrl(): string | null {
    const selectedJob = this.facade.selectedJob();

    if (!selectedJob) {
      return null;
    }

    return selectedJob.applicationUrl ?? selectedJob.sourceUrl;
  }

  ngOnInit(): void {
    this.applyRouteParams(this.route.snapshot.queryParamMap, { triggerSearch: true });

    this.route.queryParamMap
      .pipe(
        skip(1),
        filter(() => !this.suppressUrlSync),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((params) => {
        this.applyRouteParams(params, { triggerSearch: true });
      });
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
      if (selected && selected !== this.facade.selectedJobId()) {
        this.facade.select(selected);
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
