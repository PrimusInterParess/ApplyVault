import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { JobResultViewModel } from '../models/job-result-view.model';

@Component({
  selector: 'app-job-result-detail',
  imports: [DatePipe, DecimalPipe],
  template: `
    @if (job(); as selectedJob) {
      <section class="job-detail">
        <div class="job-detail__header">
          <div>
            <p class="job-detail__eyebrow">{{ selectedJob.sourceHostname }} · {{ selectedJob.detectedPageType }}</p>
            <h2>{{ selectedJob.title }}</h2>
            <p class="job-detail__company">{{ selectedJob.company }}</p>
          </div>

          <a [href]="selectedJob.url" target="_blank" rel="noreferrer">Open source listing</a>
        </div>

        <div class="job-detail__chips">
          <span>{{ selectedJob.location }}</span>
          <span>Saved {{ selectedJob.savedAt | date: 'MMM d, y, h:mm a' }}</span>
          <span>Extracted {{ selectedJob.extractedAt | date: 'MMM d, y, h:mm a' }}</span>
          <span>{{ selectedJob.textLength | number }} chars captured</span>
        </div>

        <div class="job-detail__grid">
          <article>
            <h3>Role summary</h3>
            <p>{{ selectedJob.summary }}</p>
          </article>

          <article>
            <h3>Hiring manager</h3>
            <p>{{ selectedJob.hiringManagerName }}</p>

            @if (selectedJob.hiringManagerContacts.length > 0) {
              <ul>
                @for (contact of selectedJob.hiringManagerContacts; track contact.type + contact.value) {
                  <li>
                    <span>{{ contact.label || contact.type }}</span>
                    <strong>{{ contact.value }}</strong>
                  </li>
                }
              </ul>
            } @else {
              <p class="job-detail__muted">No hiring manager contacts were captured for this result.</p>
            }
          </article>
        </div>

        <article class="job-detail__description">
          <h3>Description snapshot</h3>
          <p>{{ selectedJob.description }}</p>
        </article>
      </section>
    } @else {
      <section class="job-detail job-detail--empty">
        <p>Select a result from the list to inspect its captured details.</p>
      </section>
    }
  `,
  styleUrl: './job-result-detail.component.scss'
})
export class JobResultDetailComponent {
  readonly job = input<JobResultViewModel | null>(null);
}
