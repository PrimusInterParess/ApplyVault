import { DatePipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';

import { ExternalJobListing } from '../../models/external-job.model';
import { getJobSearchProvider, JobSearchSource } from '../../models/job-source.model';
import { formatEuresPublicationDate } from '../../utils/eures-date.util';

@Component({
  selector: 'app-external-job-card',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './external-job-card.component.html',
  styleUrl: './external-job-card.component.scss'
})
export class ExternalJobCardComponent {
  readonly job = input.required<ExternalJobListing>();
  readonly source = input.required<JobSearchSource>();
  readonly selected = input(false);
  readonly saved = input(false);
  readonly choose = output<string>();

  protected readonly postedAt = computed(() =>
    formatEuresPublicationDate(this.job().publicationDate)
  );

  protected readonly monogram = computed(() => getJobSearchProvider(this.source()).monogram);

  protected readonly sourceLabel = computed(() => getJobSearchProvider(this.source()).label);
}
