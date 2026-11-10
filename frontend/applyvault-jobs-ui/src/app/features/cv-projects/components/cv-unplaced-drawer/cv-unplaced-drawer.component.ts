import { Component, input, output, signal } from '@angular/core';

@Component({
  selector: 'app-cv-unplaced-drawer',
  standalone: true,
  templateUrl: './cv-unplaced-drawer.component.html',
  styleUrl: './cv-unplaced-drawer.component.scss'
})
export class CvUnplacedDrawerComponent {
  readonly open = input(false);
  readonly docked = input(false);
  readonly items = input<readonly string[]>([]);
  readonly busy = input(false);
  readonly error = input<string | null>(null);

  readonly closePanel = output<void>();
  readonly dismissItem = output<number>();
  readonly dismissAll = output<void>();

  protected readonly copiedIndex = signal<number | null>(null);
  private copyResetTimer: ReturnType<typeof setTimeout> | null = null;

  protected async copyItem(index: number, text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.copiedIndex.set(index);

      if (this.copyResetTimer) {
        clearTimeout(this.copyResetTimer);
      }

      this.copyResetTimer = setTimeout(() => {
        this.copiedIndex.set(null);
        this.copyResetTimer = null;
      }, 1500);
    } catch {
      this.copiedIndex.set(null);
    }
  }
}
