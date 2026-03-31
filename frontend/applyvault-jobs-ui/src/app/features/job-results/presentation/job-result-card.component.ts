import { DatePipe } from '@angular/common';
import { Component, input, output } from '@angular/core';

import { JobResultViewModel } from '../models/job-result-view.model';

@Component({
  selector: 'app-job-result-card',
  imports: [DatePipe],
  template: `
    <button
      type="button"
      class="job-card"
      [class.job-card--selected]="selected()"
      (click)="choose.emit(job().id)">
      <div class="job-card__eyebrow">
        <span>{{ job().sourceHostname }}</span>
        <span>{{ job().savedAt | date: 'MMM d, y' }}</span>
      </div>

      <h3>{{ job().title }}</h3>
      <p class="job-card__company">{{ job().company }}</p>

      <div class="job-card__meta">
        <span>{{ job().location }}</span>
        <span>{{ job().detectedPageType }}</span>
      </div>

      <p class="job-card__excerpt">{{ job().excerpt }}</p>
    </button>
  `,
  styleUrl: './job-result-card.component.scss'
})
export class JobResultCardComponent {
  readonly job = input.required<JobResultViewModel>();
  readonly selected = input(false);
  readonly choose = output<string>();
}
