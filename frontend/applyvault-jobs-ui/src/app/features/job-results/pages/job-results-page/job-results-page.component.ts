import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';

import { JobResultsFacade } from '../../data-access/job-results.facade';
import { UpdateJobCaptureReviewRequest } from '../../models/job-result.model';
import { JobResultCardComponent } from '../../presentation/job-result-card/job-result-card.component';
import {
  JobCaptureReviewSaveEvent,
  JobResultDetailComponent
} from '../../presentation/job-result-detail/job-result-detail.component';
import { CalendarConnectionsFacade } from '../../../settings/data-access/calendar-connections.facade';

@Component({
  selector: 'app-job-results-page',
  standalone: true,
  imports: [CommonModule, JobResultCardComponent, JobResultDetailComponent],
  templateUrl: './job-results-page.component.html',
  styleUrl: './job-results-page.component.scss'
})
export class JobResultsPageComponent {
  readonly facade = inject(JobResultsFacade);
  protected readonly calendarConnections = inject(CalendarConnectionsFacade);

  protected asValue(event: Event): string {
    return (event.target as HTMLInputElement | HTMLSelectElement | null)?.value ?? '';
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
