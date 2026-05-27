import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { isRequestAborted } from '../../../core/http/is-request-aborted';
import { GitHubConnectionsFacade } from '../../settings/data-access/github-connections.facade';
import { CvProjectSummary, GitHubRepositoryListItem } from '../models/cv-project.model';
import { CvProjectsApiService } from './cv-projects-api.service';

export const CV_PROJECT_REPOS_PER_PAGE = 5;
export const CV_PROJECT_SUMMARIES_PER_PAGE = 5;

@Injectable({ providedIn: 'root' })
export class CvProjectsFacade {
  private readonly authService = inject(AuthService);
  private readonly gitHubConnections = inject(GitHubConnectionsFacade);
  private readonly apiService = inject(CvProjectsApiService);
  private loadReposSubscription: Subscription | null = null;
  private loadMoreReposSubscription: Subscription | null = null;
  private loadSummariesSubscription: Subscription | null = null;
  private loadMoreSummariesSubscription: Subscription | null = null;
  private loadReadmeSubscription: Subscription | null = null;
  private generateSubscription: Subscription | null = null;
  private deleteSubscription: Subscription | null = null;
  private loadedUserId: string | null = null;
  private readonly reposLoadAttempted = signal(false);

  readonly loadingRepos = signal(false);
  readonly loadingMoreRepos = signal(false);
  readonly loadMoreReposError = signal<string | null>(null);
  readonly loadingSummaries = signal(false);
  readonly loadingMoreSummaries = signal(false);
  readonly loadMoreSummariesError = signal<string | null>(null);
  readonly generatingFullName = signal<string | null>(null);
  readonly deletingSummaryId = signal<string | null>(null);
  readonly reposError = signal<string | null>(null);
  readonly summariesError = signal<string | null>(null);
  readonly generateError = signal<string | null>(null);
  readonly loadingReadmeFullName = signal<string | null>(null);
  readonly readmeByFullName = signal<ReadonlyMap<string, string | null>>(new Map());
  readonly readmeErrorsByFullName = signal<ReadonlyMap<string, string>>(new Map());
  readonly repos = signal<readonly GitHubRepositoryListItem[]>([]);
  readonly savedSummaries = signal<readonly CvProjectSummary[]>([]);
  readonly repoPage = signal(1);
  readonly hasMoreRepos = signal(true);
  readonly summaryPage = signal(1);
  readonly hasMoreSummaries = signal(true);

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
          return;
        }

        if (!connected) {
          this.reposLoadAttempted.set(false);
          return;
        }

        if (
          !connectionsLoading &&
          !this.reposLoadAttempted() &&
          !this.loadingRepos() &&
          !this.loadingMoreRepos()
        ) {
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

    this.loadMoreReposError.set(null);
    this.loadReposSubscription = this.apiService.listRepositories(1, CV_PROJECT_REPOS_PER_PAGE).subscribe({
      next: (repos) => {
        const items = repos ?? [];

        this.repos.set(items);
        this.hasMoreRepos.set(items.length >= CV_PROJECT_REPOS_PER_PAGE);
        this.loadingRepos.set(false);
        this.loadReposSubscription = null;
      },
      error: (error) => {
        if (isRequestAborted(error)) {
          this.loadingRepos.set(false);
          this.loadReposSubscription = null;
          return;
        }

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
    this.loadMoreReposError.set(null);
    this.cancelLoadMoreRepos();

    this.loadMoreReposSubscription = this.apiService
      .listRepositories(nextPage, CV_PROJECT_REPOS_PER_PAGE)
      .subscribe({
        next: (repos) => {
          if (repos === null) {
            this.loadingMoreRepos.set(false);
            this.loadMoreReposSubscription = null;
            return;
          }

          this.repoPage.set(nextPage);
          this.repos.update((current) => [...current, ...repos]);
          this.hasMoreRepos.set(repos.length >= CV_PROJECT_REPOS_PER_PAGE);
          this.loadingMoreRepos.set(false);
          this.loadMoreReposSubscription = null;
        },
        error: (error) => {
          if (isRequestAborted(error)) {
            this.loadingMoreRepos.set(false);
            this.loadMoreReposSubscription = null;
            return;
          }

          this.loadMoreReposError.set(this.readErrorMessage(error, 'More repositories could not be loaded.'));
          this.loadingMoreRepos.set(false);
          this.loadMoreReposSubscription = null;
        }
      });
  }

  loadSummaries(): void {
    this.cancelLoadSummaries();
    this.loadingSummaries.set(true);
    this.summariesError.set(null);
    this.summaryPage.set(1);
    this.hasMoreSummaries.set(true);
    this.loadMoreSummariesError.set(null);

    this.loadSummariesSubscription = this.apiService
      .listSummaries(1, CV_PROJECT_SUMMARIES_PER_PAGE)
      .subscribe({
        next: (summaries) => {
          this.savedSummaries.set(summaries);
          this.hasMoreSummaries.set(summaries.length >= CV_PROJECT_SUMMARIES_PER_PAGE);
          this.loadingSummaries.set(false);
          this.loadSummariesSubscription = null;
        },
        error: (error) => {
          if (isRequestAborted(error)) {
            this.loadingSummaries.set(false);
            this.loadSummariesSubscription = null;
            return;
          }

          this.summariesError.set('Saved project summaries could not be loaded.');
          this.savedSummaries.set([]);
          this.loadingSummaries.set(false);
          this.loadSummariesSubscription = null;
        }
      });
  }

  loadMoreSummaries(): void {
    if (this.loadingMoreSummaries() || this.loadingSummaries() || !this.hasMoreSummaries()) {
      return;
    }

    const nextPage = this.summaryPage() + 1;
    this.loadingMoreSummaries.set(true);
    this.loadMoreSummariesError.set(null);

    this.cancelLoadMoreSummaries();
    this.loadMoreSummariesSubscription = this.apiService
      .listSummaries(nextPage, CV_PROJECT_SUMMARIES_PER_PAGE)
      .subscribe({
        next: (summaries) => {
          if (summaries === null) {
            this.loadingMoreSummaries.set(false);
            this.loadMoreSummariesSubscription = null;
            return;
          }

          this.summaryPage.set(nextPage);
          this.savedSummaries.update((current) => [...current, ...summaries]);
          this.hasMoreSummaries.set(summaries.length >= CV_PROJECT_SUMMARIES_PER_PAGE);
          this.loadingMoreSummaries.set(false);
          this.loadMoreSummariesSubscription = null;
        },
        error: (error) => {
          if (isRequestAborted(error)) {
            this.loadingMoreSummaries.set(false);
            this.loadMoreSummariesSubscription = null;
            return;
          }

          this.loadMoreSummariesError.set(
            this.readErrorMessage(error, 'More saved summaries could not be loaded.')
          );
          this.loadingMoreSummaries.set(false);
          this.loadMoreSummariesSubscription = null;
        }
      });
  }

  loadReadme(fullName: string): void {
    if (this.readmeByFullName().has(fullName)) {
      return;
    }

    this.cancelLoadReadme();
    this.loadingReadmeFullName.set(fullName);
    this.readmeErrorsByFullName.update((current) => {
      const next = new Map(current);
      next.delete(fullName);
      return next;
    });

    this.loadReadmeSubscription = this.apiService.getRepositoryReadme(fullName).subscribe({
      next: (readme) => {
        if (this.loadingReadmeFullName() !== fullName) {
          return;
        }

        this.readmeByFullName.update((current) => {
          const next = new Map(current);
          const text = readme.text?.trim();
          next.set(fullName, text ? text : null);
          return next;
        });
        this.loadingReadmeFullName.set(null);
        this.loadReadmeSubscription = null;
      },
      error: (error) => {
        if (this.loadingReadmeFullName() !== fullName) {
          return;
        }

        this.readmeErrorsByFullName.update((current) => {
          const next = new Map(current);
          next.set(
            fullName,
            this.readErrorMessage(error, 'The repository README could not be loaded.')
          );
          return next;
        });
        this.loadingReadmeFullName.set(null);
        this.loadReadmeSubscription = null;
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

  clearGenerateError(): void {
    this.generateError.set(null);
  }

  private cancelLoadRepos(): void {
    this.loadReposSubscription?.unsubscribe();
    this.loadReposSubscription = null;
  }

  private cancelLoadMoreRepos(): void {
    this.loadMoreReposSubscription?.unsubscribe();
    this.loadMoreReposSubscription = null;
  }

  private cancelLoadSummaries(): void {
    this.loadSummariesSubscription?.unsubscribe();
    this.loadSummariesSubscription = null;
  }

  private cancelLoadMoreSummaries(): void {
    this.loadMoreSummariesSubscription?.unsubscribe();
    this.loadMoreSummariesSubscription = null;
  }

  private cancelLoadReadme(): void {
    this.loadReadmeSubscription?.unsubscribe();
    this.loadReadmeSubscription = null;
  }

  private resetState(): void {
    this.cancelLoadRepos();
    this.cancelLoadMoreRepos();
    this.cancelLoadSummaries();
    this.cancelLoadMoreSummaries();
    this.cancelLoadReadme();
    this.generateSubscription?.unsubscribe();
    this.generateSubscription = null;
    this.deleteSubscription?.unsubscribe();
    this.deleteSubscription = null;
    this.loadingRepos.set(false);
    this.loadingMoreRepos.set(false);
    this.loadMoreReposError.set(null);
    this.loadingSummaries.set(false);
    this.loadingMoreSummaries.set(false);
    this.loadMoreSummariesError.set(null);
    this.generatingFullName.set(null);
    this.deletingSummaryId.set(null);
    this.reposError.set(null);
    this.summariesError.set(null);
    this.generateError.set(null);
    this.loadingReadmeFullName.set(null);
    this.readmeByFullName.set(new Map());
    this.readmeErrorsByFullName.set(new Map());
    this.repos.set([]);
    this.savedSummaries.set([]);
    this.repoPage.set(1);
    this.hasMoreRepos.set(true);
    this.summaryPage.set(1);
    this.hasMoreSummaries.set(true);
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
