import { Component, computed, effect, input, output, signal } from '@angular/core';

import { InterviewEvent, UpdateInterviewEventRequest } from '../../models/job-result.model';
import {
  fromDateTimeLocalValue,
  toDateTimeLocalValue
} from '../../utils/interview-event';

@Component({
  selector: 'app-job-result-interview-event-editor',
  standalone: true,
  templateUrl: './job-result-interview-event-editor.component.html',
  styleUrl: './job-result-interview-event-editor.component.scss'
})
export class JobResultInterviewEventEditorComponent {
  private readonly defaultTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  readonly interviewEvent = input<InterviewEvent | null>(null);
  readonly updating = input(false);
  readonly save = output<UpdateInterviewEventRequest>();
  readonly clear = output<void>();
  readonly editing = signal(false);
  readonly start = signal('');
  readonly end = signal('');
  readonly timeZone = signal(this.defaultTimeZone);
  readonly location = signal('');
  readonly notes = signal('');
  readonly canSave = computed(() => {
    const startUtc = fromDateTimeLocalValue(this.start(), this.timeZone());
    const endUtc = fromDateTimeLocalValue(this.end(), this.timeZone());

    return Boolean(
      this.editing() &&
        !this.updating() &&
        startUtc &&
        endUtc &&
        endUtc > startUtc &&
        this.timeZone().trim().length > 0
    );
  });

  constructor() {
    effect(() => {
      const interviewEvent = this.interviewEvent();

      this.start.set(toDateTimeLocalValue(interviewEvent?.startUtc ?? null));
      this.end.set(toDateTimeLocalValue(interviewEvent?.endUtc ?? null));
      this.timeZone.set(interviewEvent?.timeZone ?? this.defaultTimeZone);
      this.location.set(interviewEvent?.location ?? '');
      this.notes.set(interviewEvent?.notes ?? '');
      this.editing.set(false);
    });
  }

  beginEdit(): void {
    if (this.updating()) {
      return;
    }

    this.editing.set(true);
  }

  cancelEdit(): void {
    const interviewEvent = this.interviewEvent();
    this.start.set(toDateTimeLocalValue(interviewEvent?.startUtc ?? null));
    this.end.set(toDateTimeLocalValue(interviewEvent?.endUtc ?? null));
    this.timeZone.set(interviewEvent?.timeZone ?? this.defaultTimeZone);
    this.location.set(interviewEvent?.location ?? '');
    this.notes.set(interviewEvent?.notes ?? '');
    this.editing.set(false);
  }

  submit(): void {
    const startUtc = fromDateTimeLocalValue(this.start(), this.timeZone());
    const endUtc = fromDateTimeLocalValue(this.end(), this.timeZone());

    if (!this.canSave() || !startUtc || !endUtc) {
      return;
    }

    this.save.emit({
      startUtc,
      endUtc,
      timeZone: this.timeZone().trim(),
      location: this.location().trim() || null,
      notes: this.notes().trim() || null
    });
  }

  clearEvent(): void {
    if (!this.interviewEvent() || this.updating()) {
      return;
    }

    this.clear.emit();
  }

  updateStart(value: string): void {
    this.start.set(value);
  }

  updateEnd(value: string): void {
    this.end.set(value);
  }

  updateTimeZone(value: string): void {
    this.timeZone.set(value);
  }

  updateLocation(value: string): void {
    this.location.set(value);
  }

  updateNotes(value: string): void {
    this.notes.set(value);
  }
}
