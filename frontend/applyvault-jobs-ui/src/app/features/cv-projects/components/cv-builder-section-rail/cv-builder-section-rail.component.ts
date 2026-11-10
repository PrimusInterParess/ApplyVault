import { Component, input, output } from '@angular/core';

export interface CvBuilderRailItem {
  readonly id: string;
  readonly heading: string;
  readonly complete: boolean;
  /** Contact is always on the page and has no switch. */
  readonly canToggle: boolean;
  readonly included: boolean;
}

@Component({
  selector: 'app-cv-builder-section-rail',
  standalone: true,
  templateUrl: './cv-builder-section-rail.component.html',
  styleUrl: './cv-builder-section-rail.component.scss'
})
export class CvBuilderSectionRailComponent {
  readonly items = input<readonly CvBuilderRailItem[]>([]);
  readonly activeId = input<string | null>(null);

  readonly jumpToSection = output<string>();
  readonly sectionVisibility = output<{ id: string; included: boolean }>();

  protected itemLabel(item: CvBuilderRailItem): string {
    const filled = item.complete ? 'complete' : 'not filled';
    if (!item.canToggle) {
      return `${item.heading}, ${filled}`;
    }

    const visibility = item.included ? 'on CV' : 'off CV';
    return `${item.heading}, ${filled}, ${visibility}`;
  }

  protected visibilityLabel(item: CvBuilderRailItem): string {
    return item.included ? `Omit ${item.heading} from CV` : `Include ${item.heading} on CV`;
  }

  protected toggleVisibility(item: CvBuilderRailItem): void {
    this.sectionVisibility.emit({ id: item.id, included: !item.included });
  }
}
