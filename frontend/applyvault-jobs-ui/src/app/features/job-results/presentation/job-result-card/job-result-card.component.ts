import { DatePipe } from '@angular/common';
import { Component, input, output } from '@angular/core';

import { JobResultViewModel } from '../../models/job-result-view.model';
import { formatInterviewDate } from '../../utils/interview-date';

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
  readonly formatInterviewDate = formatInterviewDate;
}
