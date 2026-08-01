import { computed, effect, inject, Injectable, signal } from '@angular/core';

import { CvStructuredSection } from '../models/cv-structured.model';
import {
  applyCvTemplateInlineEdit,
  CvTemplateInlineEdit
} from '../utils/cv-template-inline-edit.util';
import { sectionsAreEqual } from '../utils/cv-structured-draft.util';
import { CvStructuredFacade } from './cv-structured.facade';

/**
 * Owns Structured CV draft lifecycle for the edit desk: inline draft, edit/save
 * generations, debounce/flush persist, and saveStatus. Page remains the shell.
 */
@Injectable({ providedIn: 'root' })
export class CvEditSession {
  private readonly cvStructured = inject(CvStructuredFacade);

  readonly inlineDraft = signal<CvStructuredSection[] | null>(null);

  readonly serverSections = computed(() => {
    const items = this.cvStructured.structured()?.sections ?? [];
    return [...items].sort((left, right) => left.sortOrder - right.sortOrder);
  });

  readonly sections = computed(() => this.inlineDraft() ?? this.serverSections());

  readonly saveStatus = computed(() => {
    if (this.cvStructured.isSaving()) {
      return 'Saving…';
    }

    if (this.inlineDraft()) {
      return 'Unsaved changes';
    }

    return 'Saved';
  });

  private saveTimer: ReturnType<typeof setTimeout> | null = null;
  private wasSavingSection = false;
  /** Local edit generation; cleared only when matching save generation succeeds. */
  private editGeneration = 0;
  private editGenerationAtLastSaveRequest = 0;
  private lastSaveRequestGeneration = 0;
  private seenSuccessfulSaveGeneration = 0;

  constructor() {
    effect(() => {
      const saving = this.cvStructured.isSaving();
      const saveError = this.cvStructured.saveError();
      const successfulGeneration = this.cvStructured.lastSuccessfulSaveGeneration();
      const finishedSave = this.wasSavingSection && !saving;

      // Clear draft on successful latest save generation when no newer local edit exists.
      if (
        successfulGeneration > 0 &&
        successfulGeneration !== this.seenSuccessfulSaveGeneration &&
        successfulGeneration === this.lastSaveRequestGeneration &&
        this.editGeneration === this.editGenerationAtLastSaveRequest
      ) {
        this.seenSuccessfulSaveGeneration = successfulGeneration;
        this.inlineDraft.set(null);
      } else if (finishedSave && !saveError) {
        // Secondary: equality clear for edge cases (AI/import paths) after normalize harden.
        const draft = this.inlineDraft();

        if (!draft || sectionsAreEqual(draft, this.serverSections())) {
          this.inlineDraft.set(null);
        }
      }

      this.wasSavingSection = saving;
    });
  }

  /** Apply a canvas/structure edit into the draft and schedule debounced persist. */
  apply(edit: CvTemplateInlineEdit): CvStructuredSection[] {
    const next = applyCvTemplateInlineEdit(this.sections(), edit);
    this.setDraft(next);
    this.scheduleSave(next);
    return next;
  }

  /**
   * Replace draft and persist immediately (no debounce). Used by project import.
   * Returns the structured facade save generation.
   */
  setDraftAndPersistNow(sections: CvStructuredSection[]): number {    this.cancelPendingSave();
    this.setDraft(sections);
    return this.persistSections(sections);
  }

  flushSave(): void {
    const draft = this.inlineDraft();

    this.cancelPendingSave();

    if (draft) {
      this.persistSections(draft);
    }
  }

  clearDraft(): void {
    this.inlineDraft.set(null);
  }

  cancelPendingSave(): void {
    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
      this.saveTimer = null;
    }
  }

  private setDraft(sections: CvStructuredSection[]): void {
    this.editGeneration++;
    this.inlineDraft.set(sections);
  }

  private scheduleSave(sections: CvStructuredSection[]): void {
    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
    }

    this.saveTimer = setTimeout(() => {
      this.persistSections(sections);
    }, 500);
  }

  private persistSections(sections: CvStructuredSection[]): number {
    const anchorSectionId = sections[0]?.id;

    if (!anchorSectionId) {
      return 0;
    }

    this.editGenerationAtLastSaveRequest = this.editGeneration;
    this.lastSaveRequestGeneration = this.cvStructured.save(sections, anchorSectionId);
    return this.lastSaveRequestGeneration;
  }
}
