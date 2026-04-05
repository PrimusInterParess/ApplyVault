import { DatePipe, TitleCasePipe } from '@angular/common';
import { Component, computed, effect, input, output, signal } from '@angular/core';
import { marked } from 'marked';

import { JobResultViewModel } from '../../models/job-result-view.model';
import { CaptureQualityField, UpdateInterviewEventRequest } from '../../models/job-result.model';
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

export interface JobCaptureReviewSaveEvent {
  readonly id: string;
  readonly jobTitle: string | null;
  readonly companyName: string | null;
  readonly location: string | null;
  readonly jobDescription: string | null;
}

@Component({
  selector: 'app-job-result-detail',
  standalone: true,
  imports: [DatePipe, TitleCasePipe, JobResultInterviewEventEditorComponent],
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
  readonly saveCaptureReview = output<JobCaptureReviewSaveEvent>();
  readonly saveInterviewEvent = output<JobInterviewEventSaveEvent>();
  readonly clearInterviewEvent = output<string>();
  readonly createCalendarEvent = output<{ id: string; connectedAccountId: string }>();
  readonly editingCaptureReview = signal(false);
  readonly jobTitleDraft = signal('');
  readonly companyNameDraft = signal('');
  readonly locationDraft = signal('');
  readonly editingDescription = signal(false);
  readonly descriptionDraft = signal('');
  readonly normalizedJobTitleDraft = computed(() => this.normalizeOptionalDraft(this.jobTitleDraft()));
  readonly normalizedCompanyNameDraft = computed(() => this.normalizeOptionalDraft(this.companyNameDraft()));
  readonly normalizedLocationDraft = computed(() => this.normalizeOptionalDraft(this.locationDraft()));
  readonly normalizedDraftDescription = computed(() => this.descriptionDraft().trim());
  readonly hasCaptureReviewChanges = computed(() => {
    const job = this.job();

    if (!job) {
      return false;
    }

    return (
      this.normalizedJobTitleDraft() !== this.normalizeOptionalDraft(job.captureQuality.jobTitle.effectiveValue) ||
      this.normalizedCompanyNameDraft() !== this.normalizeOptionalDraft(job.captureQuality.companyName.effectiveValue) ||
      this.normalizedLocationDraft() !== this.normalizeOptionalDraft(job.captureQuality.location.effectiveValue)
    );
  });
  readonly canSaveCaptureReview = computed(() => {
    return Boolean(
      this.job() &&
        this.editingCaptureReview() &&
        !this.updating() &&
        this.hasCaptureReviewChanges()
    );
  });
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
      const job = this.job();
      const description = job?.description ?? '';

      this.jobTitleDraft.set(job?.captureQuality.jobTitle.effectiveValue ?? '');
      this.companyNameDraft.set(job?.captureQuality.companyName.effectiveValue ?? '');
      this.locationDraft.set(job?.captureQuality.location.effectiveValue ?? '');
      this.editingCaptureReview.set(false);
      this.descriptionDraft.set(description);
      this.editingDescription.set(false);
    });
  }

  beginCaptureReviewEdit(): void {
    const job = this.job();

    if (!job || this.updating()) {
      return;
    }

    this.jobTitleDraft.set(job.captureQuality.jobTitle.effectiveValue ?? '');
    this.companyNameDraft.set(job.captureQuality.companyName.effectiveValue ?? '');
    this.locationDraft.set(job.captureQuality.location.effectiveValue ?? '');
    this.editingCaptureReview.set(true);
  }

  cancelCaptureReviewEdit(): void {
    const job = this.job();

    this.jobTitleDraft.set(job?.captureQuality.jobTitle.effectiveValue ?? '');
    this.companyNameDraft.set(job?.captureQuality.companyName.effectiveValue ?? '');
    this.locationDraft.set(job?.captureQuality.location.effectiveValue ?? '');
    this.editingCaptureReview.set(false);
  }

  updateCaptureReviewDraft(field: 'jobTitle' | 'companyName' | 'location', event: Event): void {
    const value = (event.target as HTMLInputElement | null)?.value ?? '';

    switch (field) {
      case 'jobTitle':
        this.jobTitleDraft.set(value);
        return;
      case 'companyName':
        this.companyNameDraft.set(value);
        return;
      case 'location':
        this.locationDraft.set(value);
        return;
    }
  }

  submitCaptureReview(): void {
    const job = this.job();

    if (!job || !this.canSaveCaptureReview()) {
      return;
    }

    this.saveCaptureReview.emit({
      id: job.id,
      jobTitle: this.normalizedJobTitleDraft(),
      companyName: this.normalizedCompanyNameDraft(),
      location: this.normalizedLocationDraft(),
      jobDescription: job.captureQuality.jobDescription.effectiveValue
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

  captureReviewVisible(): boolean {
    const job = this.job();
    return !!job && (job.captureQuality.needsReview || job.captureQuality.reviewStatus === 'reviewed');
  }

  describeCaptureField(field: CaptureQualityField): string {
    const confidence = `${Math.round(field.confidence * 100)}% confidence`;

    if (field.needsReview && field.reviewReason) {
      return `${confidence}. ${field.reviewReason}`;
    }

    if (field.userOverrideValue) {
      return `${confidence}. Reviewed and overridden.`;
    }

    return confidence;
  }

  describeStatusSync(): string | null {
    const statusSync = this.job()?.statusSync;

    if (!statusSync) {
      return null;
    }

    if (statusSync.source === 'gmail') {
      const when = statusSync.emailReceivedAt ?? statusSync.updatedAt;
      const timestamp = new Date(when).toLocaleString();
      const summary =
        statusSync.kind === 'interview'
          ? `Interview details auto-synced from Gmail on ${timestamp}.`
          : `Rejection status auto-synced from Gmail on ${timestamp}.`;
      const emailFrom = statusSync.emailFrom ? ` From: ${statusSync.emailFrom}.` : '';
      const subject = statusSync.emailSubject ? ` Subject: ${statusSync.emailSubject}.` : '';
      return `${summary}${emailFrom}${subject}`;
    }

    if (statusSync.source === 'manual') {
      const timestamp = new Date(statusSync.updatedAt).toLocaleString();
      return `Last ${statusSync.kind} update was saved manually on ${timestamp}.`;
    }

    return null;
  }

  private normalizeOptionalDraft(value: string | null | undefined): string | null {
    const normalized = value?.trim() ?? '';
    return normalized.length > 0 ? normalized : null;
  }
}
