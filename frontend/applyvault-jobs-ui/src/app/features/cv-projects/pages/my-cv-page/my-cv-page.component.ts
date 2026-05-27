import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal, viewChild, ElementRef } from '@angular/core';

import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import { CvDocumentFacade } from '../../data-access/cv-document.facade';

@Component({
  selector: 'app-my-cv-page',
  standalone: true,
  imports: [CommonModule, DatePipe, SkeletonBlockComponent],
  templateUrl: './my-cv-page.component.html',
  styleUrl: './my-cv-page.component.scss'
})
export class MyCvPageComponent {
  protected readonly cvDocument = inject(CvDocumentFacade);
  protected readonly deleteConfirmOpen = signal(false);
  protected readonly cvFileInput = viewChild<ElementRef<HTMLInputElement>>('cvFileInput');

  protected formatFileSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }

    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }

    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  protected openCvFilePicker(): void {
    this.cvFileInput()?.nativeElement.click();
  }

  protected onCvFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    input.value = '';

    if (!file) {
      return;
    }

    this.cvDocument.upload(file);
  }

  protected beginDeleteCv(): void {
    this.deleteConfirmOpen.set(true);
  }

  protected cancelDeleteCv(): void {
    this.deleteConfirmOpen.set(false);
  }

  protected confirmDeleteCv(): void {
    this.cvDocument.delete();
    this.deleteConfirmOpen.set(false);
  }
}
