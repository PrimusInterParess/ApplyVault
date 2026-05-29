import { Component, input, output } from '@angular/core';

import {
  JOB_SEARCH_PROVIDERS,
  JobSearchSource
} from '../../models/job-source.model';

@Component({
  selector: 'app-job-search-source-toggle',
  standalone: true,
  templateUrl: './job-search-source-toggle.component.html',
  styleUrl: './job-search-source-toggle.component.scss'
})
export class JobSearchSourceToggleComponent {
  readonly providers = JOB_SEARCH_PROVIDERS;
  readonly source = input.required<JobSearchSource>();
  readonly sourceChange = output<JobSearchSource>();

  protected setSource(nextSource: JobSearchSource): void {
    if (nextSource !== this.source()) {
      this.sourceChange.emit(nextSource);
    }
  }
}
