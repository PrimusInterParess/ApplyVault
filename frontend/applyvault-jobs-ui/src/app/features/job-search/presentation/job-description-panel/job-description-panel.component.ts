import { Component, input } from '@angular/core';

import { SafeHtmlPipe } from '../../../../core/html/safe-html.pipe';
import { JobDescriptionDisplayMode } from '../../utils/job-description-display.util';
import { renderJobDescription } from '../../utils/job-description-render.util';

@Component({
  selector: 'app-job-description-panel',
  standalone: true,
  imports: [SafeHtmlPipe],
  templateUrl: './job-description-panel.component.html',
  styleUrl: './job-description-panel.component.scss'
})
export class JobDescriptionPanelComponent {
  readonly mode = input.required<JobDescriptionDisplayMode>();
  readonly description = input<string | null>(null);
  readonly excerpt = input<string | null>(null);
  readonly qualityReason = input<string | null>(null);
  readonly listingUrl = input<string | null>(null);

  protected readonly renderJobDescription = renderJobDescription;
}
