import { DatePipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ExternalJobDetail } from '../../models/external-job.model';
import { getJobSearchProvider, JobSearchSource } from '../../models/job-source.model';
import { JobDescriptionPanelComponent } from '../job-description-panel/job-description-panel.component';
import { resolveJobDescriptionDisplayMode } from '../../utils/job-description-display.util';
import { formatEuresPublicationDate } from '../../utils/eures-date.util';

@Component({
  selector: 'app-external-job-detail',
  standalone: true,
  imports: [DatePipe, RouterLink, JobDescriptionPanelComponent],
  templateUrl: './external-job-detail.component.html',
  styleUrl: './external-job-detail.component.scss'
})
export class ExternalJobDetailComponent {
  readonly job = input<ExternalJobDetail | null>(null);
  readonly source = input.required<JobSearchSource>();
  readonly detailLoading = input(false);
  readonly detailError = input<string | null>(null);
  readonly saving = input(false);
  readonly saveError = input<string | null>(null);
  readonly savedJobId = input<string | null>(null);
  readonly saveAlreadyExists = input(false);
  readonly detailUrl = input<string | null>(null);
  readonly save = output<void>();
  readonly retryDetail = output<void>();

  protected readonly postedAt = computed(() => {
    const job = this.job();
    return job ? formatEuresPublicationDate(job.publicationDate) : null;
  });

  protected readonly canSave = computed(() => {
    return Boolean(this.job()) && !this.saving() && !this.savedJobId();
  });

  protected readonly sourceLabel = computed(() =>
    getJobSearchProvider(this.source()).detailLabel
  );

  protected readonly descriptionDisplayMode = computed(() =>
    resolveJobDescriptionDisplayMode(this.job())
  );
}
