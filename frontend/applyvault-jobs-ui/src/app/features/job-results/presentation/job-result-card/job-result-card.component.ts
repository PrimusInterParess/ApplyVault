import { DatePipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';

import { JobResultViewModel } from '../../models/job-result-view.model';
import { resolveJobCardStatus, sourceMonogram } from '../../utils/job-result-status.util';

@Component({
  selector: 'app-job-result-card',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './job-result-card.component.html',
  styleUrl: './job-result-card.component.scss'
})
export class JobResultCardComponent {
  readonly job = input.required<JobResultViewModel>();
  readonly selected = input(false);
  readonly choose = output<string>();

  protected readonly cardStatus = computed(() => resolveJobCardStatus(this.job()));
  protected readonly sourceMonogram = sourceMonogram;
}
