import {
  Component,
  effect,
  HostListener,
  input,
  output
} from '@angular/core';
import { SafeHtml } from '@angular/platform-browser';

import { CvExportHtmlPreviewComponent } from '../cv-export-html-preview/cv-export-html-preview.component';

@Component({
  selector: 'app-cv-builder-check-export',
  standalone: true,
  imports: [CvExportHtmlPreviewComponent],
  templateUrl: './cv-builder-check-export.component.html',
  styleUrl: './cv-builder-check-export.component.scss'
})
export class CvBuilderCheckExportComponent {
  readonly open = input(false);
  readonly showingLastSavedExport = input(false);
  readonly srcdoc = input<SafeHtml | null>(null);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly notice = input<string | null>(null);

  readonly closePanel = output<void>();
  readonly refresh = output<void>();

  private triggerEl: HTMLElement | null = null;
  private wasOpen = false;

  constructor() {
    effect(() => {
      const isOpen = this.open();

      if (isOpen && !this.wasOpen) {
        const active = document.activeElement;
        this.triggerEl = active instanceof HTMLElement ? active : null;

        queueMicrotask(() => {
          const dialog = document.getElementById('cv-builder-check-export-dialog');
          dialog?.focus();
        });
      }

      if (!isOpen && this.wasOpen) {
        const trigger = this.triggerEl;
        this.triggerEl = null;

        queueMicrotask(() => {
          trigger?.focus();
        });
      }

      this.wasOpen = isOpen;
    });
  }

  @HostListener('document:keydown.escape')
  protected onDocumentEscape(): void {
    if (this.open()) {
      this.closePanel.emit();
    }
  }
}
