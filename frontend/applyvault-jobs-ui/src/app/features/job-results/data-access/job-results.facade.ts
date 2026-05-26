import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, map, of, Subject, switchMap, takeUntil } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { JobResultViewModel, JobResultsStats } from '../models/job-result-view.model';
import {
  CalendarEventLink,
  SavedJobResult,
  UpdateInterviewEventRequest,
  UpdateJobCaptureReviewRequest
} from '../models/job-result.model';
import { mapSavedJobResultToViewModel } from '../utils/job-result.mapper';
import {
  compareJobResults,
  JobResultsSortOption,
  JobWorkflowFilter,
  matchesWorkflowFilter
} from '../utils/job-result-status.util';
import { JobResultsApiService } from './job-results-api.service';

const SEARCH_DEBOUNCE_MS = 250;

@Injectable({ providedIn: 'root' })
export class JobResultsFacade {
  private readonly authService = inject(AuthService);
  private readonly apiService = inject(JobResultsApiService);
  private readonly loadIntent$ = new Subject<void>();
  private readonly loadCancel$ = new Subject<void>();
  private loadedUserId: string | null = null;

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly updateError = signal<string | null>(null);
  readonly searchTermInput = signal('');
  readonly searchTerm = signal('');
  readonly selectedSource = signal('all');
  readonly workflowFilter = signal<JobWorkflowFilter>('all');
  readonly sortOption = signal<JobResultsSortOption>('saved_desc');
  readonly selectedId = signal<string | null>(null);
  readonly pendingSelectedId = signal<string | null>(null);
  readonly updatingResultId = signal<string | null>(null);
  readonly syncingCalendarAccountId = signal<string | null>(null);
  readonly lastLoadedAt = signal<Date | null>(null);
  readonly selectionChangedNotice = signal(false);
  readonly results = signal<readonly JobResultViewModel[]>([]);

  private readonly suppressAutoSelect = signal(false);
  private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

  readonly availableSources = computed(() =>
    Array.from(new Set(this.results().map((result) => result.sourceHostname))).sort((left, right) =>
      left.localeCompare(right)
    )
  );

  readonly hasActiveFilters = computed(() => {
    return (
      this.searchTerm().trim().length > 0 ||
      this.selectedSource() !== 'all' ||
      this.workflowFilter() !== 'all'
    );
  });

  readonly filteredResults = computed(() => {
    const activeSource = this.selectedSource();
    const workflow = this.workflowFilter();
    const sort = this.sortOption();
    const term = this.searchTerm().trim().toLowerCase();

    const filtered = this.results().filter((result) => {
      const matchesSource = activeSource === 'all' || result.sourceHostname === activeSource;
      const matchesWorkflow = matchesWorkflowFilter(result, workflow);
      const matchesSearch =
        term.length === 0 ||
        result.searchText.includes(term) ||
        result.detectedPageType.toLowerCase().includes(term);

      return matchesSource && matchesWorkflow && matchesSearch;
    });

    return [...filtered].sort((left, right) => compareJobResults(left, right, sort));
  });

  readonly filterSummary = computed(() => {
    const total = this.results().length;
    const shown = this.filteredResults().length;

    if (!this.hasActiveFilters()) {
      return total === 0 ? 'No saved jobs' : `${total} saved ${total === 1 ? 'job' : 'jobs'}`;
    }

    return `Showing ${shown} of ${total}`;
  });

  readonly selectedResult = computed(() => {
    const selectedId = this.selectedId();
    const filtered = this.filteredResults();

    return filtered.find((result) => result.id === selectedId) ?? filtered[0] ?? null;
  });

  readonly selectedResultId = computed(() => this.selectedResult()?.id ?? null);

  readonly stats = computed<JobResultsStats>(() => {
    const results = this.results();
    const companyCount = new Set(results.map((result) => result.company)).size;
    const sourceCount = new Set(results.map((result) => result.sourceHostname)).size;
    const rejectedCount = results.filter((result) => result.isRejected).length;

    return {
      totalResults: results.length,
      companies: companyCount,
      sources: sourceCount,
      rejected: rejectedCount
    };
  });

  constructor() {
    const destroyRef = inject(DestroyRef);

    this.loadIntent$
      .pipe(
        switchMap(() =>
          this.apiService.getAll().pipe(
            takeUntil(this.loadCancel$),
            map((results) => ({ kind: 'success' as const, results })),
            catchError((error: unknown) => of({ kind: 'error' as const, error }))
          )
        ),
        takeUntilDestroyed(destroyRef)
      )
      .subscribe((result) => {
        if (result.kind === 'error') {
          this.error.set(
            'The dashboard could not reach the API. Make sure ApplyVault.Api is running and your session is valid.'
          );
          this.results.set([]);
          this.loading.set(false);
          return;
        }

        const viewModels = [...result.results]
          .map(mapSavedJobResultToViewModel)
          .sort(
            (left, right) =>
              new Date(right.savedAt).getTime() - new Date(left.savedAt).getTime()
          );

        this.results.set(viewModels);
        this.lastLoadedAt.set(new Date());
        this.loading.set(false);
      });

    effect(
      () => {
        const session = this.authService.session();
        const currentUserId = this.authService.currentUser()?.id ?? null;

        if (!session) {
          this.loadedUserId = null;
          this.cancelPendingLoad();
          this.resetState();
          return;
        }

        if (!currentUserId) {
          this.cancelPendingLoad();
          this.resetState();
          this.loading.set(true);
          return;
        }

        if (this.loadedUserId === currentUserId) {
          return;
        }

        this.loadedUserId = currentUserId;
        this.resetState();
        this.load();
      },
      { allowSignalWrites: true }
    );

    effect(
      () => {
        const filtered = this.filteredResults();
        const selectedId = this.selectedId();

        if (filtered.length === 0) {
          if (selectedId !== null) {
            this.selectedId.set(null);
          }

          return;
        }

        if (this.suppressAutoSelect() && selectedId === null) {
          return;
        }

        const hasSelection = selectedId !== null && filtered.some((result) => result.id === selectedId);

        if (!hasSelection) {
          if (selectedId !== null) {
            this.selectionChangedNotice.set(true);
          }

          this.suppressAutoSelect.set(false);
          this.selectedId.set(filtered[0].id);
        }
      },
      { allowSignalWrites: true }
    );

    effect(
      () => {
        const pendingId = this.pendingSelectedId();

        if (!pendingId || this.loading()) {
          return;
        }

        const hasResult = this.results().some((result) => result.id === pendingId);

        if (hasResult) {
          this.selectedId.set(pendingId);
          this.pendingSelectedId.set(null);
        }
      },
      { allowSignalWrites: true }
    );

  }

  load(): void {
    this.cancelPendingLoad();
    this.loading.set(true);
    this.error.set(null);
    this.updateError.set(null);

    this.loadIntent$.next();
  }

  updateSearchTerm(value: string): void {
    this.searchTermInput.set(value);

    if (this.searchDebounceHandle !== null) {
      clearTimeout(this.searchDebounceHandle);
    }

    this.searchDebounceHandle = setTimeout(() => {
      this.searchTerm.set(value);
      this.searchDebounceHandle = null;
    }, SEARCH_DEBOUNCE_MS);
  }

  clearSearchTerm(): void {
    if (this.searchDebounceHandle !== null) {
      clearTimeout(this.searchDebounceHandle);
      this.searchDebounceHandle = null;
    }

    this.searchTermInput.set('');
    this.searchTerm.set('');
  }

  updateSource(value: string): void {
    this.selectedSource.set(value);
  }

  updateWorkflowFilter(value: JobWorkflowFilter): void {
    this.workflowFilter.set(value);
  }

  updateSortOption(value: JobResultsSortOption): void {
    this.sortOption.set(value);
  }

  clearFilters(): void {
    this.clearSearchTerm();
    this.selectedSource.set('all');
    this.workflowFilter.set('all');
    this.sortOption.set('saved_desc');
  }

  dismissSelectionChangedNotice(): void {
    this.selectionChangedNotice.set(false);
  }

  select(id: string): void {
    this.selectionChangedNotice.set(false);
    this.suppressAutoSelect.set(false);
    this.selectedId.set(id);
  }

  clearSelection(): void {
    this.suppressAutoSelect.set(true);
    this.selectedId.set(null);
  }

  selectWhenLoaded(id: string): void {
    const normalizedId = id.trim();

    if (!normalizedId) {
      return;
    }

    if (this.results().some((result) => result.id === normalizedId)) {
      this.selectedId.set(normalizedId);
      this.pendingSelectedId.set(null);
      return;
    }

    this.pendingSelectedId.set(normalizedId);

    if (!this.loading()) {
      this.load();
    }
  }

  toggleRejected(id: string): void {
    const currentResult = this.results().find((result) => result.id === id);

    if (!currentResult || this.updatingResultId() === id) {
      return;
    }

    this.updatingResultId.set(id);
    this.updateError.set(null);

    this.apiService.setRejected(id, !currentResult.isRejected).subscribe({
      next: (updatedResult) => {
        this.results.update((results) => this.replaceResult(results, updatedResult));
        this.updateError.set(null);
        this.updatingResultId.set(null);
      },
      error: () => {
        this.updateError.set('The rejection state could not be updated. Please try again.');
        this.updatingResultId.set(null);
      }
    });
  }

  deleteResult(id: string): void {
    const currentResult = this.results().find((result) => result.id === id);

    if (!currentResult || this.updatingResultId() === id) {
      return;
    }

    this.updatingResultId.set(id);
    this.updateError.set(null);

    this.apiService.delete(id).subscribe({
      next: () => {
        this.results.update((results) => results.filter((result) => result.id !== id));
        this.updateError.set(null);
        this.updatingResultId.set(null);
      },
      error: () => {
        this.updateError.set('The result could not be deleted. Please try again.');
        this.updatingResultId.set(null);
      }
    });
  }

  updateDescription(id: string, description: string): void {
    const currentResult = this.results().find((result) => result.id === id);
    const normalizedDescription = description.trim();

    if (
      !currentResult ||
      this.updatingResultId() === id ||
      normalizedDescription.length === 0 ||
      normalizedDescription === currentResult.description.trim()
    ) {
      return;
    }

    this.updatingResultId.set(id);
    this.updateError.set(null);

    this.apiService.updateDescription(id, { description: normalizedDescription }).subscribe({
      next: (updatedResult) => {
        this.results.update((results) => this.replaceResult(results, updatedResult));
        this.updateError.set(null);
        this.updatingResultId.set(null);
      },
      error: () => {
        this.updateError.set('The description could not be updated. Please try again.');
        this.updatingResultId.set(null);
      }
    });
  }

  updateCaptureReview(id: string, request: UpdateJobCaptureReviewRequest): void {
    const currentResult = this.results().find((result) => result.id === id);
    const normalizedRequest: UpdateJobCaptureReviewRequest = {
      jobTitle: request.jobTitle?.trim() || null,
      companyName: request.companyName?.trim() || null,
      location: request.location?.trim() || null,
      jobDescription: request.jobDescription?.trim() || null
    };

    if (!currentResult || this.updatingResultId() === id) {
      return;
    }

    const hasChanges =
      normalizedRequest.jobTitle !== currentResult.captureQuality.jobTitle.effectiveValue ||
      normalizedRequest.companyName !== currentResult.captureQuality.companyName.effectiveValue ||
      normalizedRequest.location !== currentResult.captureQuality.location.effectiveValue ||
      normalizedRequest.jobDescription !== currentResult.captureQuality.jobDescription.effectiveValue;

    if (!hasChanges) {
      return;
    }

    this.updatingResultId.set(id);
    this.updateError.set(null);

    this.apiService.updateCaptureReview(id, normalizedRequest).subscribe({
      next: (updatedResult) => {
        this.results.update((results) => this.replaceResult(results, updatedResult));
        this.updateError.set(null);
        this.updatingResultId.set(null);
      },
      error: () => {
        this.updateError.set('The capture review changes could not be saved. Please try again.');
        this.updatingResultId.set(null);
      }
    });
  }

  updateInterviewEvent(id: string, request: UpdateInterviewEventRequest): void {
    const currentResult = this.results().find((result) => result.id === id);

    if (
      !currentResult ||
      this.updatingResultId() === id ||
      request.endUtc <= request.startUtc
    ) {
      return;
    }

    this.updatingResultId.set(id);
    this.updateError.set(null);

    this.apiService.updateInterviewEvent(id, request).subscribe({
      next: (updatedResult) => {
        this.results.update((results) => this.replaceResult(results, updatedResult));
        this.updateError.set(null);
        this.updatingResultId.set(null);
      },
      error: () => {
        this.updateError.set('The interview event could not be updated. Please try again.');
        this.updatingResultId.set(null);
      }
    });
  }

  clearInterviewEvent(id: string): void {
    const currentResult = this.results().find((result) => result.id === id);

    if (!currentResult || this.updatingResultId() === id || !currentResult.interviewEvent) {
      return;
    }

    this.updatingResultId.set(id);
    this.updateError.set(null);

    this.apiService.clearInterviewEvent(id).subscribe({
      next: (updatedResult) => {
        this.results.update((results) => this.replaceResult(results, updatedResult));
        this.updateError.set(null);
        this.updatingResultId.set(null);
      },
      error: () => {
        this.updateError.set('The interview event could not be cleared. Please try again.');
        this.updatingResultId.set(null);
      }
    });
  }

  createCalendarEvent(resultId: string, connectedAccountId: string): void {
    const currentResult = this.results().find((result) => result.id === resultId);

    if (!currentResult?.interviewEvent || this.syncingCalendarAccountId() === connectedAccountId) {
      return;
    }

    this.syncingCalendarAccountId.set(connectedAccountId);
    this.updateError.set(null);

    this.apiService
      .createCalendarEvent(resultId, { connectedAccountId })
      .subscribe({
        next: (response) => {
          if (this.isSavedJobResult(response)) {
            this.results.update((results) => this.replaceResult(results, response));
          } else if (this.isCalendarEventLink(response)) {
            this.results.update((results) =>
              this.mergeCalendarEventLink(results, resultId, response)
            );
          } else {
            this.load();
          }

          this.updateError.set(null);
          this.syncingCalendarAccountId.set(null);
        },
        error: () => {
          this.updateError.set('The calendar event could not be created. Please try again.');
          this.syncingCalendarAccountId.set(null);
        }
      });
  }

  private mergeCalendarEventLink(
    results: readonly JobResultViewModel[],
    resultId: string,
    link: CalendarEventLink
  ): readonly JobResultViewModel[] {
    return results.map((result) => {
      if (result.id !== resultId) {
        return result;
      }

      const existingIndex = result.calendarEvents.findIndex(
        (event) => event.connectedAccountId === link.connectedAccountId
      );
      const calendarEvents =
        existingIndex >= 0
          ? result.calendarEvents.map((event, index) =>
              index === existingIndex ? link : event
            )
          : [...result.calendarEvents, link];

      return { ...result, calendarEvents };
    });
  }

  private isSavedJobResult(value: unknown): value is SavedJobResult {
    if (typeof value !== 'object' || value === null) {
      return false;
    }

    const candidate = value as Partial<SavedJobResult>;

    return (
      typeof candidate.id === 'string' &&
      typeof candidate.payload === 'object' &&
      candidate.payload !== null &&
      Array.isArray(candidate.calendarEvents)
    );
  }

  private isCalendarEventLink(value: unknown): value is CalendarEventLink {
    if (typeof value !== 'object' || value === null) {
      return false;
    }

    const candidate = value as Partial<CalendarEventLink>;

    return (
      typeof candidate.connectedAccountId === 'string' &&
      typeof candidate.externalEventId === 'string' &&
      typeof candidate.provider === 'string'
    );
  }

  private replaceResult(
    results: readonly JobResultViewModel[],
    updatedResult: SavedJobResult
  ): readonly JobResultViewModel[] {
    const updatedViewModel = mapSavedJobResultToViewModel(updatedResult);

    return results.map((result) => (result.id === updatedViewModel.id ? updatedViewModel : result));
  }

  private cancelPendingLoad(): void {
    this.loadCancel$.next();
  }

  private resetState(): void {
    this.loading.set(false);
    this.error.set(null);
    this.updateError.set(null);
    this.searchTermInput.set('');
    this.searchTerm.set('');
    this.selectedSource.set('all');
    this.workflowFilter.set('all');
    this.sortOption.set('saved_desc');
    this.selectedId.set(null);
    this.pendingSelectedId.set(null);
    this.updatingResultId.set(null);
    this.syncingCalendarAccountId.set(null);
    this.lastLoadedAt.set(null);
    this.selectionChangedNotice.set(false);
    this.suppressAutoSelect.set(false);
    this.results.set([]);

    if (this.searchDebounceHandle !== null) {
      clearTimeout(this.searchDebounceHandle);
      this.searchDebounceHandle = null;
    }
  }
}
