import { Component, input, output } from '@angular/core';

@Component({
  selector: 'app-cv-builder-empty-start',
  standalone: true,
  templateUrl: './cv-builder-empty-start.component.html',
  styleUrl: './cv-builder-empty-start.component.scss'
})
export class CvBuilderEmptyStartComponent {
  readonly startingBlank = input(false);
  readonly uploading = input(false);
  readonly startBlankError = input<string | null>(null);
  readonly uploadError = input<string | null>(null);

  readonly startBlank = output<void>();
  readonly uploadPdf = output<void>();
}
