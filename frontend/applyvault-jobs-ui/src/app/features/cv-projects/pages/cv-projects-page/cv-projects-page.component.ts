import { CommonModule, DatePipe } from '@angular/common';
import {
  afterNextRender,
  Component,
  computed,
  effect,
  ElementRef,
  EnvironmentInjector,
  HostListener,
  inject,
  runInInjectionContext,
  signal,
  viewChild
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { marked } from 'marked';

import { SafeHtmlPipe } from '../../../../core/html/safe-html.pipe';
import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import { GitHubConnectionsFacade } from '../../../settings/data-access/github-connections.facade';
import { CvProjectsFacade } from '../../data-access/cv-projects.facade';
import { CvProjectSummary, GitHubRepositoryListItem } from '../../models/cv-project.model';
import {
  hasSufficientSummaryData,
  INSUFFICIENT_SUMMARY_DATA_MESSAGE
} from '../../utils/summary-eligibility.util';

type WorkspaceMode = 'browse' | 'saved';

interface WorkspaceModeOption {
  readonly value: WorkspaceMode;
  readonly label: string;
}

@Component({
  selector: 'app-cv-projects-page',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterLink, SafeHtmlPipe, SkeletonBlockComponent],
  templateUrl: './cv-projects-page.component.html',
  styleUrl: './cv-projects-page.component.scss'
})
export class CvProjectsPageComponent {
  protected readonly facade = inject(CvProjectsFacade);
  protected readonly gitHubConnections = inject(GitHubConnectionsFacade);
  protected readonly showForksAndArchived = signal(false);
  protected readonly repoSearch = signal('');
  protected readonly workspaceMode = signal<WorkspaceMode>('browse');
  protected readonly selectedRepoId = signal<number | null>(null);
  protected readonly selectedSummaryId = signal<string | null>(null);
  protected readonly mobileDetailEngaged = signal(false);
  protected readonly listRegion = viewChild<ElementRef<HTMLElement>>('listRegion');

  protected readonly workspaceModeOptions: readonly WorkspaceModeOption[] = [
    { value: 'browse', label: 'Browse repositories' },
    { value: 'saved', label: 'Saved summaries' }
  ];

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

  protected readonly selectedRepo = computed(() => {
    const repoId = this.selectedRepoId();

    if (repoId === null) {
      return null;
    }

    return this.filteredRepos().find((repo) => repo.externalRepoId === repoId) ?? null;
  });

  protected readonly selectedSummary = computed(() => {
    const summaryId = this.selectedSummaryId();

    if (!summaryId) {
      return null;
    }

    return this.facade.savedSummaries().find((summary) => summary.id === summaryId) ?? null;
  });

  protected readonly renderedReadme = computed(() => {
    const repo = this.selectedRepo();

    if (!repo || this.savedSummaryFor(repo)) {
      return '';
    }

    const readmeText = this.readmeFor(repo)?.trim();

    if (!readmeText) {
      return '';
    }

    const rendered = marked.parse(readmeText, {
      async: false,
      breaks: true,
      gfm: true
    });

    return typeof rendered === 'string' ? rendered : '';
  });

  protected readonly skeletonRowIndexes = [0, 1, 2, 3, 4];
  protected readonly loadMoreSkeletonIndexes = [0, 1];

  private readonly injector = inject(EnvironmentInjector);
  private lastGeneratingFullName: string | null = null;

  constructor() {
    effect(() => {
      if (this.workspaceMode() !== 'browse') {
        return;
      }

      const repos = this.filteredRepos();
      const currentId = this.selectedRepoId();

      if (repos.length === 0) {
        this.selectedRepoId.set(null);
        return;
      }

      if (currentId === null || !repos.some((repo) => repo.externalRepoId === currentId)) {
        this.selectedRepoId.set(repos[0]?.externalRepoId ?? null);
      }
    });

    effect(() => {
      if (this.workspaceMode() !== 'saved') {
        return;
      }

      const summaries = this.facade.savedSummaries();
      const currentId = this.selectedSummaryId();

      if (summaries.length === 0) {
        this.selectedSummaryId.set(null);
        return;
      }

      if (currentId === null || !summaries.some((summary) => summary.id === currentId)) {
        this.selectedSummaryId.set(summaries[0]?.id ?? null);
      }
    });

    effect(() => {
      const generating = this.facade.generatingFullName();
      const previous = this.lastGeneratingFullName;
      this.lastGeneratingFullName = generating;

      if (!previous || generating) {
        return;
      }

      const summary = this.facade.savedSummaries().find((item) => item.fullName === previous);

      if (!summary) {
        return;
      }

      this.workspaceMode.set('saved');
      this.selectedSummaryId.set(summary.id);
      this.mobileDetailEngaged.set(true);
    });

    effect(() => {
      if (this.workspaceMode() !== 'browse') {
        return;
      }

      const repo = this.selectedRepo();

      if (!repo || this.savedSummaryFor(repo)) {
        return;
      }

      this.facade.loadReadme(repo.fullName);
    });

    effect(() => {
      if (this.workspaceMode() !== 'browse') {
        return;
      }

      this.facade.repos().length;
      this.facade.loadingMoreRepos();
      this.facade.loadingRepos();

      if (
        this.facade.loadingRepos() ||
        this.facade.loadingMoreRepos() ||
        !this.facade.hasMoreRepos()
      ) {
        return;
      }

      runInInjectionContext(this.injector, () => {
        afterNextRender(() => this.prefetchIfListShort());
      });
    });

    effect(() => {
      if (this.workspaceMode() !== 'saved') {
        return;
      }

      this.facade.savedSummaries().length;
      this.facade.loadingMoreSummaries();
      this.facade.loadingSummaries();

      if (
        this.facade.loadingSummaries() ||
        this.facade.loadingMoreSummaries() ||
        !this.facade.hasMoreSummaries()
      ) {
        return;
      }

      runInInjectionContext(this.injector, () => {
        afterNextRender(() => this.prefetchIfListShort());
      });
    });
  }

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

  protected savedSummaryFor(repo: GitHubRepositoryListItem): CvProjectSummary | null {
    return this.facade.savedSummaryByRepoId().get(repo.externalRepoId) ?? null;
  }

  protected isLoadingReadme(repo: GitHubRepositoryListItem): boolean {
    return this.facade.loadingReadmeFullName() === repo.fullName;
  }

  protected readmeFor(repo: GitHubRepositoryListItem): string | null | undefined {
    if (!this.facade.readmeByFullName().has(repo.fullName)) {
      return undefined;
    }

    return this.facade.readmeByFullName().get(repo.fullName) ?? null;
  }

  protected readmeErrorFor(repo: GitHubRepositoryListItem): string | null {
    return this.facade.readmeErrorsByFullName().get(repo.fullName) ?? null;
  }

  protected hasReadmeContent(repo: GitHubRepositoryListItem): boolean {
    const readmeText = this.readmeFor(repo);
    return Boolean(readmeText?.trim());
  }

  protected hasSufficientSummaryData(repo: GitHubRepositoryListItem): boolean {
    return hasSufficientSummaryData(repo, this.readmeFor(repo));
  }

  protected canGenerateSummary(repo: GitHubRepositoryListItem): boolean {
    if (this.savedSummaryFor(repo)) {
      return true;
    }

    if (this.isLoadingReadme(repo)) {
      return false;
    }

    const readmeText = this.readmeFor(repo);

    if (readmeText === undefined && !this.readmeErrorFor(repo)) {
      return false;
    }

    return hasSufficientSummaryData(repo, readmeText ?? null);
  }

  protected generateSummaryDisabledReason(repo: GitHubRepositoryListItem): string | null {
    if (this.isGenerating(repo) || this.canGenerateSummary(repo)) {
      return null;
    }

    if (this.isLoadingReadme(repo) || (this.readmeFor(repo) === undefined && !this.readmeErrorFor(repo))) {
      return 'Checking repository details before summary generation.';
    }

    return INSUFFICIENT_SUMMARY_DATA_MESSAGE;
  }

  protected readonly insufficientSummaryDataMessage = INSUFFICIENT_SUMMARY_DATA_MESSAGE;

  protected generateSummary(repo: GitHubRepositoryListItem): void {
    if (!this.canGenerateSummary(repo)) {
      return;
    }

    this.facade.generateSummary(repo.fullName);
  }

  protected isWorkspaceModeActive(mode: WorkspaceMode): boolean {
    return this.workspaceMode() === mode;
  }

  protected setWorkspaceMode(mode: WorkspaceMode): void {
    this.workspaceMode.set(mode);
    this.mobileDetailEngaged.set(false);
  }

  protected savedSummariesModeLabel(): string {
    const count = this.facade.savedSummaries().length;
    return count > 0 ? `Saved summaries (${count})` : 'Saved summaries';
  }

  protected selectRepo(repoId: number): void {
    this.facade.clearGenerateError();
    this.selectedRepoId.set(repoId);
    this.mobileDetailEngaged.set(true);
  }

  protected selectSummary(summaryId: string): void {
    this.selectedSummaryId.set(summaryId);
    this.mobileDetailEngaged.set(true);
  }

  protected isRepoSelected(repoId: number): boolean {
    return this.selectedRepoId() === repoId;
  }

  protected isSummarySelected(summaryId: string): boolean {
    return this.selectedSummaryId() === summaryId;
  }

  protected showMobileDetail(): boolean {
    if (!this.mobileDetailEngaged()) {
      return false;
    }

    if (this.workspaceMode() === 'browse') {
      return this.selectedRepoId() !== null;
    }

    return this.selectedSummaryId() !== null;
  }

  protected backToList(): void {
    this.mobileDetailEngaged.set(false);
  }

  protected loadMore(): void {
    if (this.workspaceMode() === 'browse') {
      this.facade.loadMoreRepos();
      return;
    }

    this.facade.loadMoreSummaries();
  }

  protected retryLoadMore(): void {
    this.loadMore();
  }

  protected hasMoreListItems(): boolean {
    return this.workspaceMode() === 'browse'
      ? this.facade.hasMoreRepos()
      : this.facade.hasMoreSummaries();
  }

  protected loadingMoreList(): boolean {
    return this.workspaceMode() === 'browse'
      ? this.facade.loadingMoreRepos()
      : this.facade.loadingMoreSummaries();
  }

  protected loadMoreError(): string | null {
    return this.workspaceMode() === 'browse'
      ? this.facade.loadMoreReposError()
      : this.facade.loadMoreSummariesError();
  }

  protected listItemsLoaded(): number {
    return this.workspaceMode() === 'browse'
      ? this.filteredRepos().length
      : this.facade.savedSummaries().length;
  }

  protected initialListLoading(): boolean {
    return this.workspaceMode() === 'browse'
      ? this.facade.loadingRepos()
      : this.facade.loadingSummaries();
  }

  @HostListener('window:scroll')
  protected onListScroll(): void {
    if (this.initialListLoading() || this.loadingMoreList() || !this.hasMoreListItems()) {
      return;
    }

    const thresholdPx = 160;
    const list = this.listRegion()?.nativeElement;

    if (list && list.scrollHeight > list.clientHeight + 8) {
      const listNearBottom =
        list.scrollTop + list.clientHeight >= list.scrollHeight - thresholdPx;

      if (listNearBottom) {
        this.loadMore();
      }

      return;
    }

    const documentElement = document.documentElement;
    const windowNearBottom =
      window.scrollY + window.innerHeight >= documentElement.scrollHeight - thresholdPx;

    if (windowNearBottom) {
      this.loadMore();
    }
  }

  private prefetchIfListShort(): void {
    const list = this.listRegion()?.nativeElement;

    if (!list || !this.hasMoreListItems() || this.initialListLoading() || this.loadingMoreList()) {
      return;
    }

    const listFillsViewport = list.scrollHeight <= list.clientHeight + 8;

    if (listFillsViewport) {
      this.loadMore();
    }
  }

  protected handleListKeydown(event: KeyboardEvent): void {
    const listElement = this.listRegion()?.nativeElement;

    if (!listElement || !(event.target instanceof HTMLElement) || !listElement.contains(event.target)) {
      return;
    }

    const cards = Array.from(
      listElement.querySelectorAll<HTMLButtonElement>('.cv-projects-page__list-card:not([disabled])')
    );

    if (cards.length === 0) {
      return;
    }

    const activeIndex = cards.findIndex(
      (card) =>
        card === document.activeElement || card.classList.contains('cv-projects-page__list-card--selected')
    );
    const resolvedIndex = activeIndex >= 0 ? activeIndex : 0;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.focusCard(cards, Math.min(resolvedIndex + 1, cards.length - 1));
      return;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.focusCard(cards, Math.max(resolvedIndex - 1, 0));
    }
  }

  private focusCard(cards: readonly HTMLButtonElement[], index: number): void {
    const card = cards[index];
    const repoId = card.dataset['repoId'];
    const summaryId = card.dataset['summaryId'];

    if (repoId) {
      this.selectRepo(Number(repoId));
    } else if (summaryId) {
      this.selectSummary(summaryId);
    }

    card.focus();
  }
}
