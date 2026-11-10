import { Component, input, output } from '@angular/core';

export type CvBuilderDockTool = 'assist' | 'unplaced' | 'projects';

@Component({
  selector: 'app-cv-builder-tools-dock',
  standalone: true,
  templateUrl: './cv-builder-tools-dock.component.html',
  styleUrl: './cv-builder-tools-dock.component.scss'
})
export class CvBuilderToolsDockComponent {
  readonly activeTool = input<CvBuilderDockTool | null>(null);
  readonly unplacedCount = input(0);

  readonly selectTool = output<CvBuilderDockTool>();

  protected readonly tools: readonly { id: CvBuilderDockTool; label: string }[] = [
    { id: 'assist', label: 'AI assist' },
    { id: 'unplaced', label: 'Unplaced' },
    { id: 'projects', label: 'Projects' }
  ];

  protected tabLabel(tool: { id: CvBuilderDockTool; label: string }): string {
    if (tool.id === 'unplaced' && this.unplacedCount() > 0) {
      return `Unplaced (${this.unplacedCount()})`;
    }

    return tool.label;
  }
}
