import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth/auth.service';
import { JobResultsFacade } from '../../data-access/job-results.facade';
import { JobResultCardComponent } from '../../presentation/job-result-card/job-result-card.component';
import { JobResultDetailComponent } from '../../presentation/job-result-detail/job-result-detail.component';
import { CalendarConnectionsFacade } from '../../../settings/data-access/calendar-connections.facade';

@Component({
  selector: 'app-job-results-page',
  standalone: true,
  imports: [CommonModule, RouterLink, JobResultCardComponent, JobResultDetailComponent],
  templateUrl: './job-results-page.component.html',
  styleUrl: './job-results-page.component.scss'
})
export class JobResultsPageComponent {
  protected readonly facade = inject(JobResultsFacade);
  protected readonly auth = inject(AuthService);
  protected readonly calendarConnections = inject(CalendarConnectionsFacade);

  protected asValue(event: Event): string {
    return (event.target as HTMLInputElement | HTMLSelectElement | null)?.value ?? '';
  }
}
