import { Component, computed, effect, input, output, signal } from '@angular/core';

import {
  formatInterviewDate,
  isValidInterviewDate,
  normalizeInterviewDate
} from '../../utils/interview-date';

@Component({
  selector: 'app-job-result-interview-date-editor',
  standalone: true,
  templateUrl: './job-result-interview-date-editor.component.html',
  styleUrl: './job-result-interview-date-editor.component.scss'
})
export class JobResultInterviewDateEditorComponent {
  readonly interviewDate = input<string | null>(null);
  readonly updating = input(false);
  readonly save = output<string | null>();
  readonly editing = signal(false);
  readonly draft = signal('');
  readonly normalizedDraft = computed(() => normalizeInterviewDate(this.draft()));
  readonly canSave = computed(() => {
    return (
      this.editing() &&
      !this.updating() &&
      isValidInterviewDate(this.normalizedDraft()) &&
      this.normalizedDraft() !== this.interviewDate()
    );
  });
  readonly formatInterviewDate = formatInterviewDate;

  constructor() {
    effect(() => {
      this.draft.set(this.interviewDate() ?? '');
      this.editing.set(false);
    });
  }

  beginEdit(): void {
    if (this.updating()) {
      return;
    }

    this.draft.set(this.interviewDate() ?? '');
    this.editing.set(true);
  }

  cancelEdit(): void {
    this.draft.set(this.interviewDate() ?? '');
    this.editing.set(false);
  }

  updateDraft(event: Event): void {
    const value = (event.target as HTMLInputElement | null)?.value ?? '';
    this.draft.set(value);
  }

  submit(): void {
    if (!this.canSave()) {
      return;
    }

    this.save.emit(this.normalizedDraft());
  }

  clear(): void {
    if (!this.interviewDate() || this.updating()) {
      return;
    }

    this.save.emit(null);
  }
}
