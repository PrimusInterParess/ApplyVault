import { Component, ElementRef, HostListener, inject, input, output, signal } from '@angular/core';

@Component({
  selector: 'app-cv-builder-document-menu',
  standalone: true,
  templateUrl: './cv-builder-document-menu.component.html',
  styleUrl: './cv-builder-document-menu.component.scss'
})
export class CvBuilderDocumentMenuComponent {
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly hasProfilePhoto = input(false);
  readonly photoBusy = input(false);
  readonly uploadingPdf = input(false);
  readonly deleting = input(false);

  readonly uploadPdf = output<void>();
  readonly uploadPhoto = output<void>();
  readonly removePhoto = output<void>();
  readonly deleteCv = output<void>();

  protected readonly open = signal(false);

  protected toggle(): void {
    this.open.update((value) => !value);
  }

  protected close(): void {
    this.open.set(false);
  }

  protected onUploadPdf(): void {
    this.close();
    this.uploadPdf.emit();
  }

  protected onUploadPhoto(): void {
    this.close();
    this.uploadPhoto.emit();
  }

  protected onRemovePhoto(): void {
    this.close();
    this.removePhoto.emit();
  }

  protected onDeleteCv(): void {
    this.close();
    this.deleteCv.emit();
  }

  @HostListener('document:keydown', ['$event'])
  protected onDocumentKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.close();
    }
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (!this.open()) {
      return;
    }

    const target = event.target;
    if (target instanceof Node && !this.host.nativeElement.contains(target)) {
      this.close();
    }
  }
}
