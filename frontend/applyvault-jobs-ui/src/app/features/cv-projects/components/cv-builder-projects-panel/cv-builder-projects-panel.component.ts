import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CvProjectSummary } from '../../models/cv-project.model';

@Component({
  selector: 'app-cv-builder-projects-panel',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './cv-builder-projects-panel.component.html',
  styleUrl: './cv-builder-projects-panel.component.scss'
})
export class CvBuilderProjectsPanelComponent {
  readonly open = input(false);
  readonly summaries = input<readonly CvProjectSummary[]>([]);
  readonly loadingSummaries = input(false);
  readonly summariesError = input<string | null>(null);
  readonly saveError = input<string | null>(null);
  readonly importingBusy = input(false);
  readonly selectedImportableCount = input(0);
  readonly canImportSelected = input(false);
  readonly canToggleSelection = input(false);
  readonly selectedSummaryIds = input<readonly string[]>([]);
  readonly importedSummaryIds = input<ReadonlySet<string>>(new Set());

  readonly closePanel = output<void>();
  readonly refresh = output<void>();
  readonly importSelected = output<void>();
  readonly toggleSelection = output<string>();

  protected isImported(summaryId: string): boolean {
    return this.importedSummaryIds().has(summaryId);
  }

  protected isSelected(summaryId: string): boolean {
    return this.selectedSummaryIds().includes(summaryId);
  }
}
