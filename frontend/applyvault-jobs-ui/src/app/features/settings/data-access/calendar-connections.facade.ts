import { inject, Injectable, signal } from '@angular/core';

import { CalendarConnectionsApiService } from './calendar-connections-api.service';
import { ConnectedCalendarAccount } from '../models/calendar-connection.model';

@Injectable({ providedIn: 'root' })
export class CalendarConnectionsFacade {
  private readonly apiService = inject(CalendarConnectionsApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly connectingProvider = signal<string | null>(null);
  readonly disconnectingConnectionId = signal<string | null>(null);
  readonly connections = signal<readonly ConnectedCalendarAccount[]>([]);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.apiService.getAll().subscribe({
      next: (connections) => {
        this.connections.set(connections);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Calendar connections could not be loaded.');
        this.connections.set([]);
        this.loading.set(false);
      }
    });
  }

  connect(provider: string): void {
    if (this.connectingProvider() === provider) {
      return;
    }

    this.connectingProvider.set(provider);
    this.error.set(null);

    this.apiService.startConnection(provider).subscribe({
      next: (response) => {
        window.location.assign(response.authorizationUrl);
      },
      error: () => {
        this.error.set(`The ${provider} connection flow could not be started.`);
        this.connectingProvider.set(null);
      }
    });
  }

  disconnect(id: string): void {
    if (this.disconnectingConnectionId() === id) {
      return;
    }

    this.disconnectingConnectionId.set(id);
    this.error.set(null);

    this.apiService.deleteConnection(id).subscribe({
      next: () => {
        this.connections.update((connections) => connections.filter((connection) => connection.id !== id));
        this.disconnectingConnectionId.set(null);
      },
      error: () => {
        this.error.set('The calendar connection could not be removed.');
        this.disconnectingConnectionId.set(null);
      }
    });
  }
}
