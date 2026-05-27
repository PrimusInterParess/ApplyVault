import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { GitHubConnectionsFacade } from '../../settings/data-access/github-connections.facade';
import { CvProjectSummary, GitHubRepositoryListItem } from '../models/cv-project.model';
import { CvProjectsApiService } from './cv-projects-api.service';

@Injectable({ providedIn: 'root' })
export class CvProjectsFacade {
  private readonly authService = inject(AuthService);
  private readonly gitHubConnections = inject(GitHubConnectionsFacade);
  private readonly apiService = inject(CvProjectsApiService);
  private loadReposSubscription: Subscription | null = null;
  private loadSummariesSubscription: Subscription | null = null;
  private generateSubscription: Subscription | null = null;
  private deleteSubscription: Subscription | null = null;
  private loadedUserId: string | null = null;
  private readonly reposLoadAttempted = signal(false);

  readonly loadingRepos = signal(false);
  readonly loadingMoreRepos = signal(false);
  readonly loadingSummaries = signal(false);
  readonly generatingFullName = signal<string | null>(null);
  readonly deletingSummaryId = signal<string | null>(null);
  readonly reposError = signal<string | null>(null);
  readonly summariesError = signal<string | null>(null);
  readonly generateError = signal<string | null>(null);
  readonly repos = signal<readonly GitHubRepositoryListItem[]>([]);
  readonly savedSummaries = signal<readonly CvProjectSummary[]>([]);
  readonly repoPage = signal(1);
  readonly hasMoreRepos = signal(true);

  readonly isGitHubConnected = computed(() => this.gitHubConnections.connections().length > 0);
  readonly savedSummaryByRepoId = computed(() => {
    const summaries = new Map<number, CvProjectSummary>();

    for (const summary of this.savedSummaries()) {
      summaries.set(summary.externalRepoId, summary);
    }

    return summaries;
  });

  constructor() {
    effect(
      () => {
        const session = this.authService.session();
        const currentUserId = this.authService.currentUser()?.id ?? null;
        const connectionsLoading = this.gitHubConnections.loading();
        const connected = this.isGitHubConnected();

        if (!session) {
          this.loadedUserId = null;
          this.resetState();
          return;
        }

        if (!currentUserId) {
          return;
        }

        if (this.loadedUserId !== currentUserId) {
          this.loadedUserId = currentUserId;
          this.resetState();
          this.loadSummaries();
        }

        if (!connectionsLoading && connected && !this.reposLoadAttempted() && !this.loadingRepos()) {
          this.loadRepos();
        }
      },
      { allowSignalWrites: true }
    );
  }

  loadRepos(): void {
    this.cancelLoadRepos();
    this.reposLoadAttempted.set(true);
    this.loadingRepos.set(true);
    this.reposError.set(null);
    this.repoPage.set(1);
    this.hasMoreRepos.set(true);

    this.loadReposSubscription = this.apiService.listRepositories(1).subscribe({
      next: (repos) => {
        this.repos.set(repos);
        this.hasMoreRepos.set(repos.length >= 100);
        this.loadingRepos.set(false);
        this.loadReposSubscription = null;
      },
      error: (error) => {
        this.reposError.set(this.readErrorMessage(error, 'GitHub repositories could not be loaded.'));
        this.repos.set([]);
        this.loadingRepos.set(false);
        this.loadReposSubscription = null;
      }
    });
  }

  loadMoreRepos(): void {
    if (this.loadingMoreRepos() || this.loadingRepos() || !this.hasMoreRepos()) {
      return;
    }

    const nextPage = this.repoPage() + 1;
    this.loadingMoreRepos.set(true);
    this.reposError.set(null);

    this.loadReposSubscription = this.apiService.listRepositories(nextPage).subscribe({
      next: (repos) => {
        this.repoPage.set(nextPage);
        this.repos.update((current) => [...current, ...repos]);
        this.hasMoreRepos.set(repos.length >= 100);
        this.loadingMoreRepos.set(false);
        this.loadReposSubscription = null;
      },
      error: (error) => {
        this.reposError.set(this.readErrorMessage(error, 'More repositories could not be loaded.'));
        this.loadingMoreRepos.set(false);
        this.loadReposSubscription = null;
      }
    });
  }

  loadSummaries(): void {
    this.cancelLoadSummaries();
    this.loadingSummaries.set(true);
    this.summariesError.set(null);

    this.loadSummariesSubscription = this.apiService.listSummaries().subscribe({
      next: (summaries) => {
        this.savedSummaries.set(summaries);
        this.loadingSummaries.set(false);
        this.loadSummariesSubscription = null;
      },
      error: () => {
        this.summariesError.set('Saved project summaries could not be loaded.');
        this.savedSummaries.set([]);
        this.loadingSummaries.set(false);
        this.loadSummariesSubscription = null;
      }
    });
  }

  generateSummary(fullName: string): void {
    if (this.generatingFullName() === fullName) {
      return;
    }

    this.generatingFullName.set(fullName);
    this.generateError.set(null);

    this.generateSubscription?.unsubscribe();
    this.generateSubscription = this.apiService.generateSummary({ fullName }).subscribe({
      next: (summary) => {
        this.savedSummaries.update((current) => {
          const withoutCurrent = current.filter((item) => item.externalRepoId !== summary.externalRepoId);
          return [summary, ...withoutCurrent];
        });
        this.generatingFullName.set(null);
        this.generateSubscription = null;
      },
      error: (error) => {
        this.generateError.set(
          this.readErrorMessage(error, 'The project summary could not be generated.')
        );
        this.generatingFullName.set(null);
        this.generateSubscription = null;
      }
    });
  }

  deleteSummary(id: string): void {
    if (this.deletingSummaryId() === id) {
      return;
    }

    this.deletingSummaryId.set(id);
    this.summariesError.set(null);

    this.deleteSubscription?.unsubscribe();
    this.deleteSubscription = this.apiService.deleteSummary(id).subscribe({
      next: () => {
        this.savedSummaries.update((current) => current.filter((summary) => summary.id !== id));
        this.deletingSummaryId.set(null);
        this.deleteSubscription = null;
      },
      error: () => {
        this.summariesError.set('The project summary could not be removed.');
        this.deletingSummaryId.set(null);
        this.deleteSubscription = null;
      }
    });
  }

  private cancelLoadRepos(): void {
    this.loadReposSubscription?.unsubscribe();
    this.loadReposSubscription = null;
  }

  private cancelLoadSummaries(): void {
    this.loadSummariesSubscription?.unsubscribe();
    this.loadSummariesSubscription = null;
  }

  private resetState(): void {
    this.cancelLoadRepos();
    this.cancelLoadSummaries();
    this.generateSubscription?.unsubscribe();
    this.generateSubscription = null;
    this.deleteSubscription?.unsubscribe();
    this.deleteSubscription = null;
    this.loadingRepos.set(false);
    this.loadingMoreRepos.set(false);
    this.loadingSummaries.set(false);
    this.generatingFullName.set(null);
    this.deletingSummaryId.set(null);
    this.reposError.set(null);
    this.summariesError.set(null);
    this.generateError.set(null);
    this.repos.set([]);
    this.savedSummaries.set([]);
    this.repoPage.set(1);
    this.hasMoreRepos.set(true);
    this.reposLoadAttempted.set(false);
  }

  private readErrorMessage(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const payload = (error as { error: unknown }).error;

      if (typeof payload === 'string' && payload.trim()) {
        return payload;
      }

      if (
        typeof payload === 'object' &&
        payload !== null &&
        'title' in payload &&
        typeof (payload as { title: unknown }).title === 'string'
      ) {
        return (payload as { title: string }).title;
      }
    }

    return fallback;
  }
}
