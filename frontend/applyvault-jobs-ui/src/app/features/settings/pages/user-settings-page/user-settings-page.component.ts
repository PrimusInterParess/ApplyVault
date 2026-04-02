import { CommonModule, DatePipe, TitleCasePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth/auth.service';
import { CalendarConnectionsFacade } from '../../data-access/calendar-connections.facade';

@Component({
  selector: 'app-user-settings-page',
  standalone: true,
  imports: [CommonModule, RouterLink, TitleCasePipe, DatePipe],
  templateUrl: './user-settings-page.component.html',
  styleUrl: './user-settings-page.component.scss'
})
export class UserSettingsPageComponent {
  protected readonly auth = inject(AuthService);
  protected readonly calendarConnections = inject(CalendarConnectionsFacade);
  private readonly router = inject(Router);

  protected async signOut(): Promise<void> {
    await this.auth.signOut();
    await this.router.navigateByUrl('/login');
  }
}
