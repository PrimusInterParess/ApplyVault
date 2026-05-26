import { CommonModule } from '@angular/common';
import { Component, effect, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { take } from 'rxjs';

import { JobResultsFacade } from '../../data-access/job-results.facade';
import { UpdateJobCaptureReviewRequest } from '../../models/job-result.model';
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
}

@Component({
  selector: 'app-job-results-page',
  standalone: true,
  imports: [CommonModule, JobResultCardComponent, JobResultDetailComponent, SkeletonBlockComponent],
  templateUrl: './job-results-page.component.html',
  styleUrl: './job-results-page.component.scss'
})
export class JobResultsPageComponent implements OnInit {
  readonly facade = inject(JobResultsFacade);
  readonly skeletonCardCount = [0, 1, 2, 3, 4, 5];
  readonly loadCompletionMessage = signal('');
  protected readonly deleteConfirm = signal<DeleteConfirmTarget | null>(null);
  protected readonly calendarConnections = inject(CalendarConnectionsFacade);
  protected readonly readInputValue = readInputValue;
  private readonly route = inject(ActivatedRoute);
  private wasLoading = false;

  constructor() {
    effect(() => {
      const loading = this.facade.loading();
      const error = this.facade.error();

      if (this.wasLoading && !loading && !error) {
        const count = this.facade.results().length;
        this.loadCompletionMessage.set(
          count === 0 ? 'Saved results loaded' : `Saved results loaded, ${count} jobs`
        );
      }

      this.wasLoading = loading;
    });
  }

  ngOnInit(): void {
    this.route.queryParamMap.pipe(take(1)).subscribe((params) => {
      const selectedId = params.get('selected');

      if (selectedId) {
        this.facade.selectWhenLoaded(selectedId);
      }
    });
  }

  protected beginDeleteConfirm(id: string): void {
    const job = this.facade.results().find((result) => result.id === id);

    if (!job) {
      return;
    }

    this.deleteConfirm.set({
      id: job.id,
      title: job.title
    });
  }

  protected deleteConfirmMessage(): string {
    const target = this.deleteConfirm();

    if (!target) {
      return '';
    }

    return `Delete "${target.title}"? This removes it from your review workspace and cannot be undone.`;
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
}
