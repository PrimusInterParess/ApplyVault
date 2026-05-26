import { DatePipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';

import { EuresJobListing } from '../../models/eures-job.model';
import { formatEuresPublicationDate } from '../../utils/eures-date.util';

@Component({
  selector: 'app-eures-job-card',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './eures-job-card.component.html',
  styleUrl: './eures-job-card.component.scss'
})
export class EuresJobCardComponent {
  readonly job = input.required<EuresJobListing>();
  readonly selected = input(false);
  readonly saved = input(false);
  readonly choose = output<string>();

  protected readonly postedAt = computed(() => formatEuresPublicationDate(this.job().publicationDate));
}
