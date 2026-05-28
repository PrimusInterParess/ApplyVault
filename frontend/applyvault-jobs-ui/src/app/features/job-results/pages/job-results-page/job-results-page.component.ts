import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  effect,
  ElementRef,
  HostListener,
  inject,
  OnInit,
  signal,
  viewChild
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { take } from 'rxjs';

import { JobResultsFacade } from '../../data-access/job-results.facade';
import { UpdateJobCaptureReviewRequest } from '../../models/job-result.model';
import { JobResultsSortOption, JobWorkflowFilter } from '../../utils/job-result-status.util';
import { JobResultCardComponent } from '../../presentation/job-result-card/job-result-card.component';
import {
  JobCaptureReviewSaveEvent,
  JobResultDetailComponent
} from '../../presentation/job-result-detail/job-result-detail.component';
import { readInputValue } from '../../../../core/dom/input-value.util';
import { CalendarConnectionsFacade } from '../../../settings/data-access/calendar-connections.facade';
import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';

interface DeleteConfirmTarget {
  readonly id: string;
  readonly title: string;
  readonly company: string;
}

interface WorkflowFilterOption {
  readonly value: JobWorkflowFilter;
  readonly label: string;
}

interface SortOption {
  readonly value: JobResultsSortOption;
  readonly label: string;
}

@Component({
  selector: 'app-job-results-page',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    JobResultCardComponent,
    JobResultDetailComponent,
    SkeletonBlockComponent
  ],
  templateUrl: './job-results-page.component.html',
  styleUrl: './job-results-page.component.scss'
})
export class JobResultsPageComponent implements OnInit {
  readonly facade = inject(JobResultsFacade);
  readonly skeletonCardCount = [0, 1, 2, 3, 4, 5];
  readonly workflowFilterOptions: readonly WorkflowFilterOption[] = [
    { value: 'all', label: 'All' },
    { value: 'needs_review', label: 'Needs review' },
    { value: 'interview', label: 'Interview' },
    { value: 'rejected', label: 'Rejected' },
    { value: 'hide_rejected', label: 'Hide rejected' }
  ];
  readonly sortOptions: readonly SortOption[] = [
    { value: 'saved_desc', label: 'Saved date' },
    { value: 'title_asc', label: 'Title A–Z' },
    { value: 'company_asc', label: 'Company' },
    { value: 'interview_asc', label: 'Interview date' }
  ];

  protected readonly loadBannerMessage = signal('');
  protected readonly mobileDetailEngaged = signal(false);
  protected readonly deleteConfirm = signal<DeleteConfirmTarget | null>(null);
  protected readonly calendarConnections = inject(CalendarConnectionsFacade);
  protected readonly readInputValue = readInputValue;
  protected readonly listRegion = viewChild<ElementRef<HTMLElement>>('listRegion');

  protected readonly featuredJobs = computed(() =>
    this.facade
      .results()
      .filter((job) => job.captureQuality.needsReview || job.interviewEvent !== null)
      .slice(0, 2)
  );

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private wasLoading = false;
  private bannerDismissHandle: ReturnType<typeof setTimeout> | null = null;
  private skipNextQuerySync = false;

  constructor() {
    effect(() => {
      const loading = this.facade.loading();
      const error = this.facade.error();

      if (this.wasLoading && !loading && !error) {
        const count = this.facade.results().length;
        this.showLoadBanner(
          count === 0 ? 'Saved results loaded' : `${count} saved ${count === 1 ? 'job' : 'jobs'} loaded`
        );
      }

      this.wasLoading = loading;
    });

    effect(() => {
      const selectedId = this.facade.selectedResultId();

      if (this.skipNextQuerySync) {
        this.skipNextQuerySync = false;
        return;
      }

      const currentSelected = this.route.snapshot.queryParamMap.get('selected');

      if (selectedId === currentSelected || (!selectedId && !currentSelected)) {
        return;
      }

      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: selectedId ? { selected: selectedId } : { selected: null },
        queryParamsHandling: 'merge',
        replaceUrl: true
      });
    });
  }

  ngOnInit(): void {
    this.route.queryParamMap.pipe(take(1)).subscribe((params) => {
      const selectedId = params.get('selected');

      if (selectedId) {
        this.skipNextQuerySync = true;
        this.mobileDetailEngaged.set(true);
        this.facade.selectWhenLoaded(selectedId);
      }
    });
  }

  protected lastLoadedLabel(): string | null {
    const loadedAt = this.facade.lastLoadedAt();

    if (!loadedAt) {
      return null;
    }

    return `Last updated ${loadedAt.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}`;
  }

  protected dismissLoadBanner(): void {
    this.loadBannerMessage.set('');
  }

  protected isWorkflowFilterActive(value: JobWorkflowFilter): boolean {
    return this.facade.workflowFilter() === value;
  }

  protected setWorkflowFilter(value: JobWorkflowFilter): void {
    this.facade.updateWorkflowFilter(value);
  }

  protected setSortOption(value: string): void {
    const allowed: readonly JobResultsSortOption[] = [
      'saved_desc',
      'title_asc',
      'company_asc',
      'interview_asc'
    ];

    if (allowed.includes(value as JobResultsSortOption)) {
      this.facade.updateSortOption(value as JobResultsSortOption);
    }
  }

  protected clearSearch(): void {
    this.facade.clearSearchTerm();
  }

  protected clearAllFilters(): void {
    this.facade.clearFilters();
  }

  protected showMobileDetail(): boolean {
    return this.mobileDetailEngaged() && this.facade.selectedResultId() !== null;
  }

  protected backToList(): void {
    this.mobileDetailEngaged.set(false);
    this.facade.clearSelection();
  }

  protected selectResult(id: string): void {
    this.mobileDetailEngaged.set(true);
    this.facade.select(id);
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

    const activeIndex = cards.findIndex((card) => card === document.activeElement || card.classList.contains('job-card--selected'));
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
      return;
    }

  }

  @HostListener('document:keydown', ['$event'])
  protected onDocumentKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape' && this.deleteConfirm()) {
      event.preventDefault();
      this.cancelDeleteConfirm();
    }
  }

  protected beginDeleteConfirm(id: string): void {
    const job = this.facade.results().find((result) => result.id === id);

    if (!job) {
      return;
    }

    this.deleteConfirm.set({
      id: job.id,
      title: job.title,
      company: job.company
    });
  }

  protected deleteConfirmMessage(): string {
    const target = this.deleteConfirm();

    if (!target) {
      return '';
    }

    return `Delete "${target.title}" at ${target.company}? This removes it from your review workspace and cannot be undone.`;
  }

  protected confirmDelete(): void {
    const target = this.deleteConfirm();

    if (!target) {
      return;
    }

    this.facade.deleteResult(target.id);
    this.deleteConfirm.set(null);
  }

  protected cancelDeleteConfirm(): void {
    this.deleteConfirm.set(null);
  }

  protected handleCaptureReviewSave(event: JobCaptureReviewSaveEvent): void {
    const request: UpdateJobCaptureReviewRequest = {
      jobTitle: event.jobTitle,
      companyName: event.companyName,
      location: event.location,
      jobDescription: event.jobDescription
    };

    this.facade.updateCaptureReview(event.id, request);
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

  private focusCard(cards: readonly HTMLButtonElement[], index: number): void {
    const card = cards[index];
    const jobId = card.dataset['jobId'];

    if (jobId) {
      this.facade.select(jobId);
    }

    card.focus();
  }
}
