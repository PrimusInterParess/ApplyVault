import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, effect, inject, signal, viewChild, ElementRef } from '@angular/core';

import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import { CvStructuredSectionPanelComponent } from '../../components/cv-structured-section-panel/cv-structured-section-panel.component';
import { CvDocumentFacade } from '../../data-access/cv-document.facade';
import { CvStructuredFacade } from '../../data-access/cv-structured.facade';
import { CvStructuredSection } from '../../models/cv-structured.model';
import {
  cloneSectionForDraft,
  mergeSection,
  sectionEquals
} from '../../utils/cv-structured-draft.util';

@Component({
  selector: 'app-my-cv-page',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    SkeletonBlockComponent,
    CvStructuredSectionPanelComponent
  ],
  templateUrl: './my-cv-page.component.html',
  styleUrl: './my-cv-page.component.scss'
})
export class MyCvPageComponent {
  protected readonly cvDocument = inject(CvDocumentFacade);
  protected readonly cvStructured = inject(CvStructuredFacade);
  protected readonly deleteConfirmOpen = signal(false);
  protected readonly editingSectionId = signal<string | null>(null);
  protected readonly sectionDraft = signal<CvStructuredSection | null>(null);
  protected readonly cvFileInput = viewChild<ElementRef<HTMLInputElement>>('cvFileInput');

  protected readonly extractionStatus = computed(() => this.cvDocument.importSummary());

  protected readonly sections = computed(() => {
    const items = this.cvStructured.structured()?.sections ?? [];

    return [...items].sort((left, right) => left.sortOrder - right.sortOrder);
  });

  protected readonly hasSections = computed(() => this.sections().length > 0);

  protected readonly canSaveSection = computed(() => {
    const sectionId = this.editingSectionId();
    const draft = this.sectionDraft();

    if (!sectionId || !draft) {
      return false;
    }

    const savedSection = this.sections().find((section) => section.id === sectionId);

    if (!savedSection) {
      return false;
    }

    return !sectionEquals(draft, savedSection);
  });

  protected readonly structuredReloadKey = computed(() => {
    const document = this.cvDocument.document();
    const importSummary = this.cvDocument.importSummary();

    return [
      document?.hasStructuredContent ?? false,
      document?.structuredImportedAt ?? '',
      importSummary?.sectionCount ?? 0,
      importSummary?.succeeded ?? false
    ].join('|');
  });

  private lastSavingSectionId: string | null = null;

  constructor() {
    effect(() => {
      this.structuredReloadKey();

      if (!this.cvDocument.loading() && this.cvDocument.hasDocument()) {
        this.cvStructured.load();
      }
    });

    effect(() => {
      this.structuredReloadKey();
      this.cancelSectionEdit();
    });

    effect(() => {
      const savingSectionId = this.cvStructured.savingSectionId();
      const saveError = this.cvStructured.saveError();

      if (this.lastSavingSectionId && !savingSectionId && !saveError) {
        this.cancelSectionEdit();
      }

      this.lastSavingSectionId = savingSectionId;
    });
  }

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

  protected isEditingSection(sectionId: string): boolean {
    return this.editingSectionId() === sectionId;
  }

  protected isSavingSection(sectionId: string): boolean {
    return this.cvStructured.savingSectionId() === sectionId;
  }

  protected sectionSaveError(sectionId: string): string | null {
    return this.isEditingSection(sectionId) ? this.cvStructured.saveError() : null;
  }

  protected beginSectionEdit(section: CvStructuredSection): void {
    if (this.cvStructured.savingSectionId() || this.editingSectionId()) {
      return;
    }

    this.cvStructured.clearSaveError();
    this.editingSectionId.set(section.id);
    this.sectionDraft.set(cloneSectionForDraft(section));
  }

  protected cancelSectionEdit(): void {
    if (this.cvStructured.savingSectionId()) {
      return;
    }

    this.editingSectionId.set(null);
    this.sectionDraft.set(null);
    this.cvStructured.clearSaveError();
  }

  protected saveSectionEdit(): void {
    const sectionId = this.editingSectionId();
    const draft = this.sectionDraft();

    if (!sectionId || !draft || !this.canSaveSection()) {
      return;
    }

    this.cvStructured.save(mergeSection(this.sections(), draft), sectionId);
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
