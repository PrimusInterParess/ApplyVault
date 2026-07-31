import { Component, input } from '@angular/core';
import { SafeHtml } from '@angular/platform-browser';

export type CvExportHtmlPreviewDensity = 'default' | 'stage' | 'thumb';

/**
 * M1 strategy A: sandboxed iframe fidelity preview (no allow-scripts).
 * Parent must not assign full CV HTML via innerHTML — use srcdoc only.
 */
@Component({
  selector: 'app-cv-export-html-preview',
  standalone: true,
  templateUrl: './cv-export-html-preview.component.html',
  styleUrl: './cv-export-html-preview.component.scss',
  host: {
    '[class.cv-html-preview-host--stage]': 'density() === "stage"',
    '[class.cv-html-preview-host--thumb]': 'density() === "thumb"'
  }
})
export class CvExportHtmlPreviewComponent {
  readonly srcdoc = input<SafeHtml | null>(null);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly emptyHint = input('Save your CV sections to load the export preview.');
  /**
   * thumb: first-page A4 crop (overflow hidden).
   * stage: scaled A4-width viewport with scroll for multi-page export HTML.
   * default: edit-canvas fidelity iframe (scrollable, unscaled width).
   */
  readonly density = input<CvExportHtmlPreviewDensity>('default');
}
