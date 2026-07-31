import { Component, input } from '@angular/core';
import { SafeHtml } from '@angular/platform-browser';

/**
 * M1 strategy A: sandboxed iframe fidelity preview (no allow-scripts).
 * Parent must not assign full CV HTML via innerHTML — use srcdoc only.
 */
@Component({
  selector: 'app-cv-export-html-preview',
  standalone: true,
  templateUrl: './cv-export-html-preview.component.html',
  styleUrl: './cv-export-html-preview.component.scss'
})
export class CvExportHtmlPreviewComponent {
  readonly srcdoc = input<SafeHtml | null>(null);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly emptyHint = input('Save your CV sections to load the export preview.');
}
