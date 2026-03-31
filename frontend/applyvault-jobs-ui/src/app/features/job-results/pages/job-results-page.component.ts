import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';

import { JobResultsFacade } from '../data-access/job-results.facade';
import { JobResultCardComponent } from '../presentation/job-result-card.component';
import { JobResultDetailComponent } from '../presentation/job-result-detail.component';

@Component({
  selector: 'app-job-results-page',
  imports: [CommonModule, JobResultCardComponent, JobResultDetailComponent],
  template: `
    <main class="jobs-page">
      <section class="jobs-page__hero">
        <div>
          <p class="jobs-page__eyebrow">ApplyVault dashboard</p>
          <h1>Saved job results, organized into a clean review workspace.</h1>
          <p class="jobs-page__intro">
            Browse every captured listing, filter by source, and inspect the structured details without digging through raw JSON.
          </p>
        </div>

        <div class="jobs-page__stats">
          <article>
            <span>Total results</span>
            <strong>{{ facade.stats().totalResults }}</strong>
          </article>
          <article>
            <span>Companies</span>
            <strong>{{ facade.stats().companies }}</strong>
          </article>
          <article>
            <span>Sources</span>
            <strong>{{ facade.stats().sources }}</strong>
          </article>
          <article>
            <span>Remote / hybrid</span>
            <strong>{{ facade.stats().remoteFriendly }}</strong>
          </article>
        </div>
      </section>

      <section class="jobs-page__toolbar">
        <label class="jobs-page__field">
          <span>Search</span>
          <input
            type="search"
            placeholder="Title, company, manager, location..."
            [value]="facade.searchTerm()"
            (input)="facade.updateSearchTerm(asValue($event))" />
        </label>

        <label class="jobs-page__field">
          <span>Source</span>
          <select [value]="facade.selectedSource()" (change)="facade.updateSource(asValue($event))">
            <option value="all">All sources</option>
            @for (source of facade.availableSources(); track source) {
              <option [value]="source">{{ source }}</option>
            }
          </select>
        </label>

        <button type="button" class="jobs-page__refresh" (click)="facade.load()">Refresh</button>
      </section>

      @if (facade.loading()) {
        <section class="jobs-page__state">
          <h2>Loading saved results...</h2>
          <p>Pulling captured job listings from the ApplyVault API.</p>
        </section>
      } @else {
        @if (facade.error(); as errorMessage) {
          <section class="jobs-page__state jobs-page__state--error">
            <h2>Dashboard unavailable</h2>
            <p>{{ errorMessage }}</p>
            <button type="button" (click)="facade.load()">Try again</button>
          </section>
        } @else {
          <section class="jobs-page__content">
            <aside class="jobs-page__list">
              @if (facade.filteredResults().length === 0) {
                <div class="jobs-page__empty">
                  <h2>No matching results</h2>
                  <p>Adjust your filters or save more jobs from the extension.</p>
                </div>
              } @else {
                @for (job of facade.filteredResults(); track job.id) {
                  <app-job-result-card
                    [job]="job"
                    [selected]="facade.selectedResultId() === job.id"
                    (choose)="facade.select($event)" />
                }
              }
            </aside>

            <section class="jobs-page__detail">
              <app-job-result-detail [job]="facade.selectedResult()" />
            </section>
          </section>
        }
      }
    </main>
  `,
  styleUrl: './job-results-page.component.scss'
})
export class JobResultsPageComponent {
  protected readonly facade = inject(JobResultsFacade);

  protected asValue(event: Event): string {
    return (event.target as HTMLInputElement | HTMLSelectElement | null)?.value ?? '';
  }
}
