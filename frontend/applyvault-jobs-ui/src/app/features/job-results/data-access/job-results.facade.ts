import { computed, effect, inject, Injectable, signal } from '@angular/core';

import { JobResultViewModel, JobResultsStats } from '../models/job-result-view.model';
import {
  ConnectedCalendarAccount,
  SavedJobResult,
  UpdateInterviewEventRequest
} from '../models/job-result.model';
import { mapSavedJobResultToViewModel } from '../utils/job-result.mapper';
import { JobResultsApiService } from './job-results-api.service';

@Injectable({ providedIn: 'root' })
export class JobResultsFacade {
  private readonly apiService = inject(JobResultsApiService);

  readonly loading = signal(true);
  readonly connectionsLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly updateError = signal<string | null>(null);
  readonly connectionError = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly selectedSource = signal('all');
  readonly selectedId = signal<string | null>(null);
  readonly updatingResultId = signal<string | null>(null);
  readonly connectingProvider = signal<string | null>(null);
  readonly syncingCalendarAccountId = signal<string | null>(null);
  readonly results = signal<readonly JobResultViewModel[]>([]);
  readonly connections = signal<readonly ConnectedCalendarAccount[]>([]);

  readonly availableSources = computed(() =>
    Array.from(new Set(this.results().map((result) => result.sourceHostname))).sort((left, right) =>
      left.localeCompare(right)
    )
  );

  readonly filteredResults = computed(() => {
    const activeSource = this.selectedSource();
    const term = this.searchTerm().trim().toLowerCase();

    return this.results().filter((result) => {
      const matchesSource = activeSource === 'all' || result.sourceHostname === activeSource;
      const matchesSearch =
        term.length === 0 ||
        result.searchText.includes(term) ||
        result.detectedPageType.toLowerCase().includes(term);

      return matchesSource && matchesSearch;
    });
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

        const hasSelection = selectedId !== null && filtered.some((result) => result.id === selectedId);

        if (!hasSelection) {
          this.selectedId.set(filtered[0].id);
        }
      },
      { allowSignalWrites: true }
    );

    this.load();
    this.loadConnections();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.updateError.set(null);

    this.apiService.getAll().subscribe({
      next: (results) => {
        const viewModels = [...results]
          .map(mapSavedJobResultToViewModel)
          .sort(
            (left, right) =>
              new Date(right.savedAt).getTime() - new Date(left.savedAt).getTime()
          );

        this.results.set(viewModels);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(
          'The dashboard could not reach the API. Make sure ApplyVault.Api is running and your session is valid.'
        );
        this.results.set([]);
        this.loading.set(false);
      }
    });
  }

  loadConnections(): void {
    this.connectionsLoading.set(true);
    this.connectionError.set(null);

    this.apiService.getCalendarConnections().subscribe({
      next: (connections) => {
        this.connections.set(connections);
        this.connectionsLoading.set(false);
      },
      error: () => {
        this.connectionError.set('Calendar connections could not be loaded.');
        this.connections.set([]);
        this.connectionsLoading.set(false);
      }
    });
  }

  updateSearchTerm(value: string): void {
    this.searchTerm.set(value);
  }

  updateSource(value: string): void {
    this.selectedSource.set(value);
  }

  select(id: string): void {
    this.selectedId.set(id);
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

  connectCalendar(provider: string): void {
    if (this.connectingProvider() === provider) {
      return;
    }

    this.connectingProvider.set(provider);
    this.connectionError.set(null);

    this.apiService.startCalendarConnection(provider).subscribe({
      next: (response) => {
        window.location.assign(response.authorizationUrl);
      },
      error: () => {
        this.connectionError.set(`The ${provider} connection flow could not be started.`);
        this.connectingProvider.set(null);
      }
    });
  }

  disconnectCalendar(id: string): void {
    this.connectionError.set(null);

    this.apiService.deleteCalendarConnection(id).subscribe({
      next: () => {
        this.connections.update((connections) => connections.filter((connection) => connection.id !== id));
      },
      error: () => {
        this.connectionError.set('The calendar connection could not be removed.');
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
        next: () => {
          this.load();
          this.syncingCalendarAccountId.set(null);
        },
        error: () => {
          this.updateError.set('The calendar event could not be created. Please try again.');
          this.syncingCalendarAccountId.set(null);
        }
      });
  }

  private replaceResult(
    results: readonly JobResultViewModel[],
    updatedResult: SavedJobResult
  ): readonly JobResultViewModel[] {
    const updatedViewModel = mapSavedJobResultToViewModel(updatedResult);

    return results.map((result) => (result.id === updatedViewModel.id ? updatedViewModel : result));
  }
}
