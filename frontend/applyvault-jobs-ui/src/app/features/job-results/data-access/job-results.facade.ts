import { computed, effect, inject, Injectable, signal } from '@angular/core';

import { JobResultViewModel, JobResultsStats } from '../models/job-result-view.model';
import { mapSavedJobResultToViewModel } from '../utils/job-result.mapper';
import { JobResultsApiService } from './job-results-api.service';

@Injectable({ providedIn: 'root' })
export class JobResultsFacade {
  private readonly apiService = inject(JobResultsApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly selectedSource = signal('all');
  readonly selectedId = signal<string | null>(null);
  readonly results = signal<readonly JobResultViewModel[]>([]);

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
    const remoteCount = results.filter((result) => /remote|hybrid/i.test(result.location)).length;

    return {
      totalResults: results.length,
      companies: companyCount,
      sources: sourceCount,
      remoteFriendly: remoteCount
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
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

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
          'The dashboard could not reach the API. Make sure ApplyVault.Api is running on http://localhost:5173.'
        );
        this.results.set([]);
        this.loading.set(false);
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
}
