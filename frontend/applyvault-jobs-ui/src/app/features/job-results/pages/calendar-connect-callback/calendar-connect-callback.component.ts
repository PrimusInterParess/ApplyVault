import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-calendar-connect-callback',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './calendar-connect-callback.component.html',
  styleUrl: './calendar-connect-callback.component.scss'
})
export class CalendarConnectCallbackComponent {
  private readonly route = inject(ActivatedRoute);

  readonly provider = this.route.snapshot.queryParamMap.get('provider') ?? 'calendar';
  readonly success = this.route.snapshot.queryParamMap.get('success') === 'true';
}
