import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import { GitHubConnectionsFacade } from '../../../settings/data-access/github-connections.facade';
import { CvProjectsFacade } from '../../data-access/cv-projects.facade';
import { GitHubRepositoryListItem } from '../../models/cv-project.model';

@Component({
  selector: 'app-cv-projects-page',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterLink, SkeletonBlockComponent],
  templateUrl: './cv-projects-page.component.html',
  styleUrl: './cv-projects-page.component.scss'
})
export class CvProjectsPageComponent {
  protected readonly facade = inject(CvProjectsFacade);
  protected readonly gitHubConnections = inject(GitHubConnectionsFacade);
  protected readonly showForksAndArchived = signal(false);
  protected readonly repoSearch = signal('');

  protected readonly filteredRepos = computed(() => {
    const query = this.repoSearch().trim().toLowerCase();

    return this.facade.repos().filter((repo) => {
      if (!this.showForksAndArchived() && (repo.isFork || repo.isArchived)) {
        return false;
      }

      if (!query) {
        return true;
      }

      return (
        repo.fullName.toLowerCase().includes(query) ||
        (repo.description?.toLowerCase().includes(query) ?? false) ||
        (repo.primaryLanguage?.toLowerCase().includes(query) ?? false)
      );
    });
  });

  protected readonly skeletonRowIndexes = [0, 1, 2];

  protected updateRepoSearch(event: Event): void {
    const target = event.target as HTMLInputElement | null;
    this.repoSearch.set(target?.value ?? '');
  }

  protected toggleForksAndArchived(): void {
    this.showForksAndArchived.update((value) => !value);
  }

  protected isGenerating(repo: GitHubRepositoryListItem): boolean {
    return this.facade.generatingFullName() === repo.fullName;
  }

  protected savedSummaryFor(repo: GitHubRepositoryListItem) {
    return this.facade.savedSummaryByRepoId().get(repo.externalRepoId) ?? null;
  }

  protected generateSummary(repo: GitHubRepositoryListItem): void {
    this.facade.generateSummary(repo.fullName);
  }
}
