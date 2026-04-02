import { DatePipe, TitleCasePipe } from '@angular/common';
import { Component, computed, effect, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { marked } from 'marked';

import { JobResultViewModel } from '../../models/job-result-view.model';
import { UpdateInterviewEventRequest } from '../../models/job-result.model';
import { formatInterviewEventWindow } from '../../utils/interview-event';
import { JobResultInterviewEventEditorComponent } from '../job-result-interview-event-editor/job-result-interview-event-editor.component';
import { ConnectedCalendarAccount } from '../../../settings/models/calendar-connection.model';

export interface JobDescriptionSaveEvent {
  readonly id: string;
  readonly description: string;
}

export interface JobInterviewEventSaveEvent {
  readonly id: string;
  readonly request: UpdateInterviewEventRequest;
}

@Component({
  selector: 'app-job-result-detail',
  standalone: true,
  imports: [DatePipe, TitleCasePipe, RouterLink, JobResultInterviewEventEditorComponent],
  templateUrl: './job-result-detail.component.html',
  styleUrl: './job-result-detail.component.scss'
})
export class JobResultDetailComponent {
  readonly job = input<JobResultViewModel | null>(null);
  readonly updating = input(false);
  readonly connections = input<readonly ConnectedCalendarAccount[]>([]);
  readonly connectionsLoading = input(false);
  readonly connectionError = input<string | null>(null);
  readonly syncingCalendarAccountId = input<string | null>(null);
  readonly toggleRejected = output<string>();
  readonly deleteResult = output<string>();
  readonly saveDescription = output<JobDescriptionSaveEvent>();
  readonly saveInterviewEvent = output<JobInterviewEventSaveEvent>();
  readonly clearInterviewEvent = output<string>();
  readonly createCalendarEvent = output<{ id: string; connectedAccountId: string }>();
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
  readonly formatInterviewEventWindow = formatInterviewEventWindow;

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

  submitInterviewEvent(request: UpdateInterviewEventRequest): void {
    const job = this.job();

    if (!job) {
      return;
    }

    this.saveInterviewEvent.emit({
      id: job.id,
      request
    });
  }

  clearScheduledInterview(): void {
    const job = this.job();

    if (!job) {
      return;
    }

    this.clearInterviewEvent.emit(job.id);
  }

  syncCalendar(connectedAccountId: string): void {
    const job = this.job();

    if (!job?.interviewEvent) {
      return;
    }

    this.createCalendarEvent.emit({
      id: job.id,
      connectedAccountId
    });
  }
}
