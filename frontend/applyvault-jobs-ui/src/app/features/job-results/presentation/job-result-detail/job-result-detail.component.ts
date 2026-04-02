import { DatePipe } from '@angular/common';
import { Component, computed, effect, input, output, signal } from '@angular/core';
import { marked } from 'marked';

import { JobResultViewModel } from '../../models/job-result-view.model';
import { formatInterviewDate } from '../../utils/interview-date';
import { JobResultInterviewDateEditorComponent } from '../job-result-interview-date-editor/job-result-interview-date-editor.component';

export interface JobDescriptionSaveEvent {
  readonly id: string;
  readonly description: string;
}

export interface JobInterviewDateSaveEvent {
  readonly id: string;
  readonly interviewDate: string | null;
}

@Component({
  selector: 'app-job-result-detail',
  standalone: true,
  imports: [DatePipe, JobResultInterviewDateEditorComponent],
  templateUrl: './job-result-detail.component.html',
  styleUrl: './job-result-detail.component.scss'
})
export class JobResultDetailComponent {
  readonly job = input<JobResultViewModel | null>(null);
  readonly updating = input(false);
  readonly toggleRejected = output<string>();
  readonly deleteResult = output<string>();
  readonly saveDescription = output<JobDescriptionSaveEvent>();
  readonly saveInterviewDate = output<JobInterviewDateSaveEvent>();
  readonly editingDescription = signal(false);
  readonly descriptionDraft = signal('');
  readonly normalizedDraftDescription = computed(() => this.descriptionDraft().trim());
  readonly hasDescriptionChanges = computed(() => {
    const currentDescription = this.job()?.description ?? '';
    return this.normalizedDraftDescription() !== currentDescription.trim();
  });
  readonly canSaveDescription = computed(() => {
    return Boolean(
      this.job() &&
        this.editingDescription() &&
        !this.updating() &&
        this.normalizedDraftDescription().length > 0 &&
        this.hasDescriptionChanges()
    );
  });
  readonly renderedDescription = computed(() => {
    const description = this.job()?.description?.trim();

    if (!description) {
      return '';
    }

    const rendered = marked.parse(description, {
      async: false,
      breaks: true,
      gfm: true
    });

    return typeof rendered === 'string' ? rendered : '';
  });
  readonly formatInterviewDate = formatInterviewDate;

  constructor() {
    effect(() => {
      const description = this.job()?.description ?? '';

      this.descriptionDraft.set(description);
      this.editingDescription.set(false);
    });
  }

  beginDescriptionEdit(): void {
    const description = this.job()?.description;

    if (!description || this.updating()) {
      return;
    }

    this.descriptionDraft.set(description);
    this.editingDescription.set(true);
  }

  cancelDescriptionEdit(): void {
    this.descriptionDraft.set(this.job()?.description ?? '');
    this.editingDescription.set(false);
  }

  updateDescriptionDraft(event: Event): void {
    const value = (event.target as HTMLTextAreaElement | null)?.value ?? '';
    this.descriptionDraft.set(value);
  }

  submitDescriptionEdit(): void {
    const job = this.job();

    if (!job || !this.canSaveDescription()) {
      return;
    }

    this.saveDescription.emit({
      id: job.id,
      description: this.normalizedDraftDescription()
    });
  }
}
