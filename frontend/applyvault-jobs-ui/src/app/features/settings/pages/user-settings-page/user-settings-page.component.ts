import { CommonModule, DatePipe, TitleCasePipe } from '@angular/common';
import { Component, inject } from '@angular/core';

import { CalendarConnectionsFacade } from '../../data-access/calendar-connections.facade';
import { MailConnectionsFacade } from '../../data-access/mail-connections.facade';

@Component({
  selector: 'app-user-settings-page',
  standalone: true,
  imports: [CommonModule, TitleCasePipe, DatePipe],
  templateUrl: './user-settings-page.component.html',
  styleUrl: './user-settings-page.component.scss'
})
export class UserSettingsPageComponent {
  protected readonly calendarConnections = inject(CalendarConnectionsFacade);
  protected readonly mailConnections = inject(MailConnectionsFacade);

  protected formatSyncStatus(value: string | null | undefined): string {
    const normalized = value?.trim();

    if (!normalized) {
      return 'Unknown';
    }

    return normalized
      .replace(/[_-]+/g, ' ')
      .replace(/\b\w/g, (character) => character.toUpperCase());
  }
}
